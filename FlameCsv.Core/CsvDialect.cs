using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv;

/// <summary>
/// Validated instance of CSV tokens.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvDialect<T> : IEquatable<CsvDialect<T>> where T : unmanaged, IEquatable<T>
{
    public static CsvDialect<T> Default => CsvDialectStatic.GetDefault<T>();

    /// <inheritdoc cref="ICsvDialectOptions{T}.Delimiter"/>
    public T Delimiter { get; internal init; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public T Quote { get; internal init; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public ReadOnlyMemory<T> Newline { get; internal init; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Escape"/>
    public T? Escape { get; internal init;  }

    public CsvDialect(ICsvDialectOptions<T> value) : this(
        delimiter: value.Delimiter,
        quote: value.Quote,
        newline: value.Newline,
        escape: value.Escape)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect(
        T delimiter,
        T quote,
        ReadOnlyMemory<T> newline,
        T? escape = null)
    {
        ThrowIfInvalid(delimiter, quote, newline.Span, escape);

        Delimiter = delimiter;
        Quote = quote;
        Newline = newline;
        Escape = escape;
    }

    public void EnsureValid() => ThrowIfInvalid(Delimiter, Quote, Newline.Span, Escape);

    public bool Equals(CsvDialect<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && Quote.Equals(other.Quote)
            && Newline.Span.SequenceEqual(other.Newline.Span)
            && Escape.HasValue.Equals(other.Escape.HasValue)
            && Escape.GetValueOrDefault().Equals(other.Escape.GetValueOrDefault());
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            Quote,
            HashCode<T>.Combine(Newline.Span),
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
        T? escape)
    {
        List<string>? errors = null;

        if (delimiter.Equals(default) && quote.Equals(default) && newline.IsEmpty && !escape.HasValue)
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

        if (errors is not null)
            CsvDialectStatic.ThrowForInvalid(errors);

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddError(string message) => (errors ??= new()).Add(message);
    }
}
