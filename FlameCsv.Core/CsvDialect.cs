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
    public T Delimiter { get; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public T Quote { get; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public ReadOnlyMemory<T> Newline { get; }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Whitespace"/>
    public ReadOnlyMemory<T> Whitespace { get; }

    public CsvDialect(ICsvDialectOptions<T> value) : this(
        delimiter: value.Delimiter,
        quote: value.Quote,
        newline: value.Newline,
        whitespace: value.Whitespace)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect(
        T delimiter,
        T quote,
        ReadOnlyMemory<T> newline,
        ReadOnlyMemory<T> whitespace)
    {
        CsvDialectStatic.ThrowIfInvalid(delimiter, quote, newline.Span, whitespace.Span);

        Delimiter = delimiter;
        Quote = quote;
        Newline = newline;
        Whitespace = whitespace;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect<T> Clone(
        T? delimiter = null,
        T? quote = null,
        ReadOnlyMemory<T>? newline = null,
        ReadOnlyMemory<T>? whitespace = null)
    {
        return new(
            delimiter ?? Delimiter,
            quote ?? Quote,
            newline ?? Newline,
            whitespace ?? Whitespace);
    }

    public void EnsureValid() => CsvDialectStatic.ThrowIfInvalid(Delimiter, Quote, Newline.Span, Whitespace.Span);

    public bool Equals(CsvDialect<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && Quote.Equals(other.Quote)
            && Newline.Span.SequenceEqual(other.Newline.Span)
            && Whitespace.Span.SequenceEqual(other.Whitespace.Span);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            Quote,
            HashCode<T>.Combine(Newline.Span),
            HashCode<T>.Combine(Whitespace.Span));
    }

    public override bool Equals(object? obj) => obj is CsvDialect<T> other && Equals(other);
    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);
}
