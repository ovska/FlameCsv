using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv;

/// <summary>
/// Validated instance of CSV tokens.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvDialect<T> : IEquatable<CsvDialect<T>> where T : unmanaged, IEquatable<T>
{
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
    public static CsvDialect<T> Default
    {
        get
        {
            if (typeof(T) == typeof(char))
                return (CsvDialect<T>)(object)CsvDialectStatic._charCRLF;

            if (typeof(T) == typeof(byte))
                return (CsvDialect<T>)(object)CsvDialectStatic._byteCRLF;

            Token<T>.ThrowNotSupportedException();
            return default;
        }
    }

    public static CsvDialect<T> Environment
    {
        get
        {
            if (typeof(T) == typeof(char))
                return (CsvDialect<T>)(object)CsvDialectStatic.Text(newline: System.Environment.NewLine);

            if (typeof(T) == typeof(byte))
                return (CsvDialect<T>)(object)CsvDialectStatic.Utf8(newline: System.Environment.NewLine);

            Token<T>.ThrowNotSupportedException();
            return default;
        }
    }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Delimiter"/>
    public readonly T Delimiter;

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public readonly T Quote;

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public readonly ReadOnlyMemory<T> Newline;

    /// <inheritdoc cref="ICsvDialectOptions{T}.Whitespace"/>
    public readonly ReadOnlyMemory<T> Whitespace;

    /// <inheritdoc cref="ICsvDialectOptions{T}.Escape"/>
    public readonly T? Escape;

    /// <summary>
    /// Returns <see langword="true"/> if the dialect is using RFC 4180 mode, which doesn't use an escape character.
    /// </summary>
    /// <seealso cref="Escape"/>
    [MemberNotNullWhen(false, nameof(Escape))]
    public bool IsRFC4180Mode => !Escape.HasValue;

    public CsvDialect(ICsvDialectOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var delimiter = options.Delimiter;
        var quote = options.Quote;
        var newline = options.Newline;
        var whitespace = options.Whitespace;
        var escape = options.Escape;

        ThrowIfInvalid(delimiter, quote, newline.Span, whitespace.Span, escape);

        Delimiter = delimiter;
        Quote = quote;
        Newline = newline;
        Escape = escape;
    }

    // perf: use the fields directly internally instead of through iface
    internal CsvDialect(CsvOptions<T> options, in CsvContextOverride<T> context)
        : this(
            delimiter: context._delimiter.Resolve(options._delimiter),
            quote: context._quote.Resolve(options._quote),
            newline: context._newline.Resolve(options._newline),
            whitespace: context._whitespace.Resolve(options._whitespace),
            escape: context._escape.Resolve(options._escape))
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect(
        T delimiter,
        T quote,
        ReadOnlyMemory<T> newline,
        ReadOnlyMemory<T> whitespace,
        T? escape)
    {
        ThrowIfInvalid(delimiter, quote, newline.Span, whitespace.Span, escape);

        Delimiter = delimiter;
        Quote = quote;
        Newline = newline;
        Whitespace = whitespace;
        Escape = escape;
    }

    public void EnsureValid() => ThrowIfInvalid(Delimiter, Quote, Newline.Span, Whitespace.Span, Escape);

    public bool Equals(CsvDialect<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && Quote.Equals(other.Quote)
            && Newline.Span.SequenceEqual(other.Newline.Span)
            && Whitespace.Equals(other.Whitespace)
            && Nullable.Equals(Escape, other.Escape);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            Quote,
            Whitespace.IsEmpty ? 0 : HashCode<T>.Combine(Whitespace.Span),
            Newline.IsEmpty ? 0 : HashCode<T>.Combine(Newline.Span),
            Escape);
    }

    public override bool Equals(object? obj) => obj is CsvDialect<T> other && Equals(other);
    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);

    [System.Diagnostics.Conditional("DEBUG")]
    internal void DebugValidate() => EnsureValid();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfInvalid(
        T delimiter,
        T quote,
        ReadOnlySpan<T> newline,
        ReadOnlySpan<T> whitespace,
        T? escape)
    {
        List<string>? errors = null;

        if (delimiter.Equals(default) &&
            quote.Equals(default) &&
            newline.IsEmpty &&
            whitespace.IsEmpty &&
            !escape.HasValue)
        {
            CsvDialectStatic.ThrowForDefault();
        }

        if (delimiter.Equals(quote))
        {
            AddError("Delimiter and Quote must not be equal.");
        }

        if (escape.HasValue)
        {
            if (escape.GetValueOrDefault().Equals(delimiter))
                AddError("Escape must not be equal to Delimiter.");

            if (escape.GetValueOrDefault().Equals(quote))
                AddError("Escape must not be equal to Quote.");
        }

        if (newline.IsEmpty)
        {
            AddError("Newline must not be empty.");
        }
        else
        {
            if (newline.Contains(delimiter))
                AddError("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                AddError("Newline must not contain Quote.");

            if (escape.HasValue && newline.Contains(escape.GetValueOrDefault()))
                AddError("Newline must not contain Escape.");
        }

        if (!whitespace.IsEmpty)
        {
            if (whitespace.Contains(delimiter))
                AddError("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(quote))
                AddError("Whitespace must not contain Quote.");

            if (escape.HasValue && whitespace.Contains(escape.GetValueOrDefault()))
                AddError("Whitespace must not contain Escape.");

            if (whitespace.IndexOfAny(newline) >= 0)
                AddError("Whitespace must not contain Newline.");
        }

        if (errors is not null)
            CsvDialectStatic.ThrowForInvalid(errors);

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddError(string message) => (errors ??= []).Add(message);
    }
}
