using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Contains the token configuration for reading and writing CSV.
/// </summary>
/// <remarks>Internal implementation detail.</remarks>
/// <typeparam name="T">Token type</typeparam>
/// <seealso cref="CsvOptions{T}.Dialect"/>
[PublicAPI]
public readonly struct CsvDialect<T>() : IEquatable<CsvDialect<T>> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The separator character between CSV fields.
    /// </summary>
    public required T Delimiter { get; init; }

    /// <summary>
    /// Characted used to quote strings containing special characters.
    /// </summary>
    public required T Quote { get; init; }

    /// <summary>
    /// 1-2 characters long newline string, or empty if newline is automatically detected
    /// (<c>CRLF</c> is used while writing in this case).
    /// </summary>
    /// <remarks>
    /// The value must not on
    /// </remarks>
    public ReadOnlySpan<T> Newline
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _newline;
        init
        {
            if (value.IsEmpty)
            {
                _newline = null;
                return;
            }

            if (value.Length == 1 && value[0] == T.CreateTruncating('\n'))
            {
                _newline = _cachedLF;
                return;
            }

            if (value.Length == 2 && value[0] == T.CreateTruncating('\r') && value[1] == T.CreateTruncating('\n'))
            {
                _newline = _cachedCRLF;
                return;
            }

            _newline = value.ToArray();
        }
    }

    /// <summary>
    /// Whitespace characters.
    /// When reading, they are trimmed out of each field before processing them.
    /// When writing, fields with the preceding or trailing whitespace are quoted if fields are automatically quoted.
    /// </summary>
    public ReadOnlySpan<T> Whitespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _whitespace;
        init
        {
            if (value.IsEmpty)
            {
                _whitespace = null;
                return;
            }

            using (ValueListBuilder<T> list = new(stackalloc T[8]))
            {
                foreach (var token in value)
                {
                    if (!list.AsSpan().Contains(token))
                    {
                        list.Append(token);
                    }
                }

                _whitespace = list.AsSpan().ToArray();
            }

            _whitespace.AsSpan().Sort();
            _whitespaceLength = _whitespace.Length;
        }
    }

    /// <summary>
    /// Optional character used for escaping special characters.
    /// </summary>
    public T? Escape { get; init; }

    private readonly LazyValues _lazyValues = new();

    internal readonly int _whitespaceLength;
    private readonly T[]? _newline;
    private readonly T[]? _whitespace;

    /// <summary>
    /// Returns the underlying storage for <see cref="Whitespace"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T[]? GetWhitespaceArray() => _whitespace;

    /// <summary>
    /// Returns a <see cref="SearchValues{T}"/> instance
    /// that contains characters that require quotes around the CSV field.
    /// </summary>
    public SearchValues<T> NeedsQuoting
    {
        get => _lazyValues.NeedsQuoting ??= GetNeedsQuoting();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _lazyValues.NeedsQuoting = value;
        }
    }

    /// <summary>
    /// Returns a <see cref="SearchValues{T}"/> instance used to find the next token in CSV.
    /// This includes the delimiter, quote, escape (if applicable), and the first newline token if
    /// <paramref name="length"/> is not 0.
    /// </summary>
    /// <param name="length">
    /// Newline length of the current read operation.
    /// Must be either the same as <see cref="Newline"/> length,
    /// or 0 if reading the final block without a detected newline.
    /// </param>
    /// <returns>Cached instance that can be used to seek the CSV.</returns>
    internal SearchValues<T> GetFindToken(int length)
    {
        return _lazyValues.FindArray[length] ??= InitializeFindToken(length);
    }

    /// <summary>
    /// Determines if all tokens in the dialect are within ASCII range.
    /// This is a requirement for high performance SIMD vectorization.
    /// If false, this will have the same effect as setting <see cref="CsvOptions{T}.NoReadAhead"/> to false.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool IsAscii => _lazyValues.IsAscii ??= GetIsAscii();

    /// <summary>
    /// Returns the newline buffer for <see cref="Newline"/>. If empty, returns <see cref="NewlineBuffer{T}.CRLF"/>
    /// if writing, and an empty buffer if reading.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NewlineBuffer<T> GetNewlineOrDefault(bool forWriting = false)
    {
        return Newline.IsEmpty
            ? forWriting ? NewlineBuffer<T>.CRLF : default
            : new NewlineBuffer<T>(Newline);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> GetNeedsQuoting()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);

        if (Newline.IsEmpty)
        {
            // crlf is the default when writing
            list.Append(NewlineBuffer<T>.CRLF.First);
            list.Append(NewlineBuffer<T>.CRLF.Second);
        }
        else
        {
            list.Append(Newline);
        }

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        return ToSearchValues(list.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> InitializeFindToken(int length)
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        if (length == 0)
        {
            if (!Newline.IsEmpty)
                Throw.Unreachable("Newline length is 0, but Newline is not empty.");
        }
        else
        {
            if (!Newline.IsEmpty)
            {
                list.Append(Newline[0]);
            }
            else if (length == 1)
            {
                list.Append(NewlineBuffer<T>.LF.First);
            }
            else if (length == 2)
            {
                list.Append(NewlineBuffer<T>.CRLF.First);
            }
        }

        return ToSearchValues(list.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool GetIsAscii()
    {
        T max = T.CreateChecked(127);

        if (Delimiter > max || Quote > max || (Escape > max)) return false;
        foreach (var c in Newline)
            if (c > max)
                return false;
        foreach (var c in Whitespace)
            if (c > max)
                return false;

        return true;
    }

    private static SearchValues<T> ToSearchValues(ReadOnlySpan<T> tokens)
    {
        if (typeof(T) == typeof(byte))
        {
            return (SearchValues<T>)(object)SearchValues.Create(tokens.Cast<T, byte>());
        }

        if (typeof(T) == typeof(char))
        {
            return (SearchValues<T>)(object)SearchValues.Create(tokens.Cast<T, char>());
        }

        throw new NotSupportedException($"SearchValues cannot be created for token {typeof(T).FullName}");
    }

    private sealed class LazyValues
    {
        public SearchValues<T>? NeedsQuoting;
        public FindTokens FindArray;
        public bool? IsAscii;

        public void Reset()
        {
            NeedsQuoting = null;
            IsAscii = null;
            FindArray = default;
        }

        [InlineArray(3)]
        public struct FindTokens
        {
            public SearchValues<T>? elem0;
        }
    }

    /// <summary>
    /// Ensures that the dialect is valid, and has no problematic overlap in the tokens.
    /// </summary>
    /// <exception cref="CsvConfigurationException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Validate()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        StringScratch scratch = default;
        ValueListBuilder<string> errors = new(scratch);

        T delimiter = Delimiter;
        T quote = Quote;
        T? escape = Escape;
        scoped ReadOnlySpan<T> whitespace = Whitespace;
        scoped ReadOnlySpan<T> newline = Newline.IsEmpty
            ? [T.CreateChecked('\r'), T.CreateChecked('\n')]
            : Newline;

        if (delimiter == T.Zero) errors.Append(("Delimiter"));
        if (quote == T.Zero) errors.Append(NullError("Quote"));
        if (escape == T.Zero) errors.Append(NullError("Escape"));

        foreach (var c in newline)
        {
            if (c == T.Zero)
            {
                errors.Append(NullError("Newline"));
                break;
            }
        }
        // allow zero in whitespace

        if (errors.Length > 0) goto CheckErrors;

        // byte dialects must be ASCII
        if (typeof(T) == typeof(byte))
        {
            T maxAscii = T.CreateChecked(127);
            if (delimiter > maxAscii) errors.Append(AsciiError("Delimiter"));
            if (quote > maxAscii) errors.Append(AsciiError("Quote"));
            if (escape > maxAscii) errors.Append(AsciiError("Escape"));
            foreach (var c in newline)
            {
                if (c > maxAscii)
                {
                    errors.Append(AsciiError("Newline"));
                    break;
                }
            }
        }

        // char dialects must not contain surrogate characters
        if (typeof(T) == typeof(char))
        {
            if (char.IsSurrogate((char)ushort.CreateTruncating(delimiter))) errors.Append(SurrogateError("Delimiter"));
            if (char.IsSurrogate((char)ushort.CreateTruncating(quote))) errors.Append(SurrogateError("Quote"));
            if (escape.HasValue && char.IsSurrogate((char)ushort.CreateTruncating(escape.Value)))
                errors.Append(SurrogateError("Escape"));

            foreach (var c in newline)
            {
                if (char.IsSurrogate((char)ushort.CreateTruncating(c)))
                {
                    errors.Append(SurrogateError("Newline"));
                    break;
                }
            }
        }

        if (delimiter.Equals(quote))
        {
            errors.Append("Delimiter and Quote must not be equal.");
        }

        if (escape.HasValue)
        {
            if (escape.GetValueOrDefault().Equals(delimiter))
                errors.Append("Escape must not be equal to Delimiter.");

            if (escape.GetValueOrDefault().Equals(quote))
                errors.Append("Escape must not be equal to Quote.");
        }

        if (newline.Length is not (1 or 2))
        {
            errors.Append("Newline must be empty, or 1 or 2 characters long.");
        }
        else
        {
            if (newline.Length == 2 && newline[0] == newline[1])
            {
                errors.Append("Newline must not contain duplicate characters.");
            }
            else
            {
                foreach (var c in newline)
                {
                    if (c == delimiter) errors.Append("Newline must not contain Delimiter.");
                    if (c == quote) errors.Append("Newline must not contain Quote.");
                    if (c == escape) errors.Append("Newline must not contain Escape.");
                }
            }
        }

        if (!whitespace.IsEmpty)
        {
            foreach (var c in whitespace)
            {
                if (c == delimiter) errors.Append("Whitespace must not contain Delimiter.");
                if (c == quote) errors.Append("Whitespace must not contain Quote.");
                if (c == escape) errors.Append("Whitespace must not contain Escape.");

                foreach (var c2 in newline)
                {
                    if (c == c2)
                    {
                        errors.Append("Whitespace must not contain Newline characters.");
                        break;
                    }
                }
            }
        }

    CheckErrors:
        using (errors)
        {
            if (errors.Length != 0)
            {
                _lazyValues.Reset(); // reset possible faulty cached value

                if (Unsafe.SizeOf<T>() is sizeof(byte) or sizeof(char))
                {
                    var vsb = new ValueStringBuilder(stackalloc char[64]);
                    vsb.Append("Tokens:");
                    SingleToken(ref vsb, "Delimiter", Delimiter);
                    SingleToken(ref vsb, "Quote", Quote);
                    SingleToken(ref vsb, "Escape", escape);
                    MultiToken(ref vsb, "Newline", newline);
                    MultiToken(ref vsb, "Whitespace", whitespace);
                    errors.Append(vsb.ToString());
                }

                InvalidDialect.Throw(errors.AsSpan());
            }
        }

        static void SingleToken(ref ValueStringBuilder vsb, ReadOnlySpan<char> name, T? value)
        {
            vsb.Append(' ');
            vsb.Append(name);
            vsb.Append(": ");
            AppendToken(ref vsb, value);
        }

        static void MultiToken(ref ValueStringBuilder vsb, ReadOnlySpan<char> name, ReadOnlySpan<T> values)
        {
            vsb.Append(' ');
            vsb.Append(name);
            vsb.Append(": ");

            if (values.IsEmpty)
            {
                vsb.Append("<empty>");
                return;
            }

            vsb.Append('[');

            for (int i = 0; i < values.Length; i++)
            {
                AppendToken(ref vsb, values[i]);
            }

            vsb.Append(']');
        }

        static void AppendToken(ref ValueStringBuilder vsb, T? value)
        {
            if (value is null)
            {
                vsb.Append("<null>");
                return;
            }

            char v = (char)ushort.CreateTruncating(value.Value);

            switch (v)
            {
                case '\0': vsb.Append(@"\0"); break;
                case '\\': vsb.Append(@"\\"); break;
                case '\r': vsb.Append(@"\r"); break;
                case '\n': vsb.Append(@"\n"); break;
                case '\t': vsb.Append(@"\t"); break;
                case ' ': vsb.Append(" "); break;
                default: vsb.Append(v); break;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string NullError(string name)
        {
            string zeroName = typeof(T) == typeof(char) ? "'\\0'" : "0";
            return $"Dialect can not contain {zeroName} in a searchable property ({name}).";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string AsciiError(string name)
        {
            return $"Dialect can not contain non-ASCII characters in a searchable property ({name}).";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string SurrogateError(string name)
        {
            return $"Dialect cannot contain surrogate characters in searchable properties ({name}).";
        }
    }

    /// <summary>
    /// Returns whether the parameter object is equal to this dialect.
    /// </summary>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CsvDialect<T> other && Equals(other);

    /// <summary>
    /// Returns whether all tokens in the dialect are equal to the other dialect.
    /// </summary>
    public bool Equals(CsvDialect<T> other)
    {
        return
            Delimiter == other.Delimiter &&
            Quote == other.Quote &&
            Escape == other.Escape &&
            Newline.SequenceEqual(other.Newline) &&
            Whitespace.SequenceEqual(other.Whitespace);
    }

    /// <summary>
    /// Returns a hash code for the dialect.
    /// </summary>
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(Delimiter);
        hash.Add(Quote);
        hash.Add(Escape);
        hash.Add(Newline.IsEmpty);
        foreach (var c in Newline) hash.Add(c);
        hash.Add(Whitespace.IsEmpty);
        foreach (var c in Whitespace) hash.Add(c);

        return hash.ToHashCode();
    }

    /// <summary></summary>
    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);

    /// <summary></summary>
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);

    private static readonly T[]? _cachedLF = _cachedLF = [T.CreateTruncating('\n')];
    private static readonly T[] _cachedCRLF = _cachedCRLF = [T.CreateTruncating('\r'), T.CreateTruncating('\n')];
}

// throwhelper doesn't need to be generic
file static class InvalidDialect
{
    [DoesNotReturn]
    [StackTraceHidden]
    public static void Throw(scoped ReadOnlySpan<string> errors)
    {
        throw new CsvConfigurationException($"Invalid CsvOptions dialect: {string.Join(" ", errors.ToArray())}");
    }
}
