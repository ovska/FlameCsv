using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv;

/// <summary>
/// Contains the token configuration for reading and writing CSV.
/// </summary>
/// <remarks>Internal implementation detail.</remarks>
/// <typeparam name="T">Token type</typeparam>
/// <seealso cref="CsvOptions{T}.Dialect"/>
[PublicAPI]
public readonly struct CsvDialect<T>() : IEquatable<CsvDialect<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvOptions{T}.Delimiter"/>
    public required T Delimiter { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Quote"/>
    public required T Quote { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Newline"/>
    public required ReadOnlySpan<T> Newline
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

            if (value.Length == 1 && value[0] == T.CreateChecked('\n'))
            {
                _newline = _cachedLF ??= [T.CreateChecked('\n')];
                return;
            }

            if (value.Length == 2 && value[0] == T.CreateChecked('\r') && value[1] == T.CreateChecked('\n'))
            {
                _newline = _cachedCRLF ??= [T.CreateChecked('\r'), T.CreateChecked('\n')];
                return;
            }

            if (value.Length is not (1 or 2))
            {
                InvalidDialect.Throw(["Newline length must be 0, 1 or 2."]);
            }

            _newline = value.ToArray();
        }
    }

    /// <inheritdoc cref="CsvOptions{T}.Whitespace"/>
    public ReadOnlySpan<T> Whitespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _whitespace;
        init => _whitespace = value.IsEmpty ? null : value.ToArray();
    }

    /// <inheritdoc cref="CsvOptions{T}.Escape"/>
    public required T? Escape { get; init; }

    private readonly LazyValues _lazyValues = new();

    private readonly T[]? _newline;
    private readonly T[]? _whitespace;

    /// <summary>
    /// Returns the underlying storage for <see cref="Whitespace"/>.
    /// </summary>
    internal T[]? GetWhitespaceArray() => _whitespace;

    /// <summary>
    /// Returns search values used to determine whether fields need to be quoted while writing CSV.
    /// </summary>
    public SearchValues<T> NeedsQuoting
    {
        get => _lazyValues.NeedsQuoting ??= GetNeedsQuoting();
        init => _lazyValues.NeedsQuoting = value;
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
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if all tokens are within ASCII range.
    /// </returns>
    /// <remarks>Required for vectorization.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
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
        bool retVal = false;
        ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);
        if (Escape.HasValue) list.Append(Escape.Value);
        if (!Newline.IsEmpty) list.Append(Newline); // empty newline is CRLF -> always ASCII
        if (!Whitespace.IsEmpty) list.Append(Whitespace);

        if (Unsafe.SizeOf<T>() == sizeof(byte))
        {
            retVal = Ascii.IsValid(list.AsSpan().Cast<T, byte>());
        }

        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            retVal = Ascii.IsValid(list.AsSpan().Cast<T, char>());
        }

        list.Dispose();
        return retVal;
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
        using ValueListBuilder<string> errors = new(scratch);

        T delimiter = Delimiter;
        T quote = Quote;
        T? escape = Escape;
        scoped ReadOnlySpan<T> whitespace = Whitespace;
        scoped ReadOnlySpan<T> newline = Newline.IsEmpty
            ? [T.CreateChecked('\r'), T.CreateChecked('\n')]
            : Newline;

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
            if (newline.Contains(delimiter))
                errors.Append("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                errors.Append("Newline must not contain Quote.");

            if (escape.HasValue && newline.Contains(escape.GetValueOrDefault()))
                errors.Append("Newline must not contain Escape.");
        }

        if (!whitespace.IsEmpty)
        {
            if (whitespace.Contains(delimiter))
                errors.Append("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(quote))
                errors.Append("Whitespace must not contain Quote.");

            if (escape.HasValue && whitespace.Contains(escape.GetValueOrDefault()))
                errors.Append("Whitespace must not contain Escape.");

            if (whitespace.IndexOfAny(newline) >= 0)
                errors.Append("Whitespace must not contain Newline characters.");
        }

        // otherwise valid, but invalid tokens for utf8
        if (errors.Length == 0 && Unsafe.SizeOf<T>() == sizeof(byte) && !GetIsAscii())
        {
            errors.Append("All tokens for byte must be valid ASCII characters.");
        }

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
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is CsvDialect<T> other && Equals(other);

    public bool Equals(CsvDialect<T> other)
    {
        return
            Delimiter == other.Delimiter &&
            Quote == other.Quote &&
            Escape == other.Escape &&
            Newline.SequenceEqual(other.Newline) &&
            Whitespace.SequenceEqual(other.Whitespace);
    }

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

    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);

    private static T[]? _cachedLF;
    private static T[]? _cachedCRLF;
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
