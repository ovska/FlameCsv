using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
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
    /// 1-2 characters long newline, or empty if newline is automatically detected.
    /// </summary>
    /// <remarks>
    /// If empty, the newline is <c>\r\n</c> when writing, and when validating the dialect.
    /// </remarks>
    public NewlineBuffer<T> Newline
    {
        get => _newline;
        init => _newline = value;
    }

    /// <summary>
    /// Wheter to trim trailing or leading spaces from unquoted fields.
    /// </summary>
    public CsvFieldTrimming Trimming
    {
        get => _trimming;
        init => _trimming = value;
    }

    /// <summary>
    /// Optional character used for escaping special characters.
    /// </summary>
    public T? Escape { get; init; }

    private readonly StrongBox<SearchValues<T>?> _lazyValues = new();

    internal readonly NewlineBuffer<T> _newline;
    internal readonly CsvFieldTrimming _trimming;

    /// <summary>
    /// Returns a <see cref="SearchValues{T}"/> instance
    /// that contains characters that require quotes around the CSV field.
    /// </summary>
    public SearchValues<T> NeedsQuoting
    {
        get => _lazyValues.Value ??= GetNeedsQuoting();
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _lazyValues.Value = value;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> GetNeedsQuoting()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);

        list.Append(Newline.First);
        list.Append(Newline.Second); // First and Second are the same on 1-char newlines

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        return ToSearchValues(list.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> InitializeFindToken()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[5]);

        list.Append(Delimiter);
        list.Append(Quote);

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        list.Append(Newline.First);
        list.Append(Newline.Second);

        return ToSearchValues(list.AsSpan());
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
        NewlineBuffer<T> newline = Newline.IsEmpty ? NewlineBuffer<T>.CRLF : Newline;

        if (delimiter == T.Zero) errors.Append(("Delimiter"));
        if (quote == T.Zero) errors.Append(NullError("Quote"));
        if (escape == T.Zero) errors.Append(NullError("Escape"));
        if (newline.First == T.Zero || newline.Second == T.Zero) errors.Append(NullError("Newline"));

        // early exit if we have nulls
        if (errors.Length > 0) goto CheckErrors;

        // dialects must be ASCII
        T maxAscii = T.CreateChecked(127);
        if (delimiter > maxAscii) errors.Append(AsciiError("Delimiter"));
        if (quote > maxAscii) errors.Append(AsciiError("Quote"));
        if (escape > maxAscii) errors.Append(AsciiError("Escape"));
        if (newline.First > maxAscii || newline.Second > maxAscii) errors.Append(AsciiError("Newline"));

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

        T f = newline.First;
        T s = newline.Second;
        if (f == delimiter || s == delimiter) errors.Append("Newline must not contain Delimiter.");
        if (f == quote || s == quote) errors.Append("Newline must not contain Quote.");
        if (f == escape || s == escape) errors.Append("Newline must not contain Escape.");

        if (Trimming != CsvFieldTrimming.None)
        {
            T space = T.CreateChecked(' ');
            if (space == delimiter) errors.Append("Delimiter must not be a space if trimming is enabled.");
            if (space == quote) errors.Append("Quote must not be a space if trimming is enabled.");
            if (space == escape) errors.Append("Escape must not be a space if trimming is enabled.");
            if (space == f || space == s) errors.Append("Newline must not contain a space if trimming is enabled.");
        }

    CheckErrors:
        if (errors.Length != 0)
        {
            _lazyValues.Value = null; // reset possible faulty cached value

            if (Unsafe.SizeOf<T>() is sizeof(byte) or sizeof(char))
            {
                var vsb = new ValueStringBuilder(stackalloc char[64]);
                vsb.Append("Tokens:");
                SingleToken(ref vsb, "Delimiter", Delimiter);
                SingleToken(ref vsb, "Quote", Quote);
                SingleToken(ref vsb, "Escape", escape);
                MultiToken(ref vsb, "Newline", MemoryMarshal.CreateReadOnlySpan(in newline.First, newline.Length));
                vsb.Append("Trimming: ");
                vsb.AppendFormatted(Trimming);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string NullError(string name)
        {
            string zeroName = typeof(T) == typeof(char) ? "'\\0'" : "0";
            return $"Dialect can not contain {zeroName} in a searchable property ({name}).";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string AsciiError(string name)
        {
            return $"Dialect can not contain non-ASCII characters ({name}).";
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
            Newline.Equals(other.Newline) &&
            Trimming == other.Trimming;
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
        hash.Add(Newline.GetHashCode());
        hash.Add(Trimming);
        return hash.ToHashCode();
    }

    /// <summary></summary>
    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);

    /// <summary></summary>
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);
}

// throwhelper doesn't need to be generic
file static class InvalidDialect
{
    [DoesNotReturn]
    [StackTraceHidden]
    public static void Throw(scoped ReadOnlySpan<string> errors)
    {
        throw new CsvConfigurationException($"Invalid CSV dialect: {string.Join(" ", errors.ToArray())}");
    }
}
