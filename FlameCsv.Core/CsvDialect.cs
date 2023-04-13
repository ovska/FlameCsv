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

    public CsvDialect(ICsvDialectOptions<T> value) : this(
        delimiter: value.Delimiter,
        quote: value.Quote,
        newline: value.Newline)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect(
        T delimiter,
        T quote,
        ReadOnlyMemory<T> newline)
    {
        CsvDialectStatic.ThrowIfInvalid(delimiter, quote, newline.Span);

        Delimiter = delimiter;
        Quote = quote;
        Newline = newline;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvDialect<T> Clone(
        T? delimiter = null,
        T? quote = null,
        ReadOnlyMemory<T>? newline = null)
    {
        return new(
            delimiter ?? Delimiter,
            quote ?? Quote,
            newline ?? Newline);
    }

    public void EnsureValid() => CsvDialectStatic.ThrowIfInvalid(Delimiter, Quote, Newline.Span);

    public bool Equals(CsvDialect<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && Quote.Equals(other.Quote)
            && Newline.Span.SequenceEqual(other.Newline.Span);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            Quote,
            HashCode<T>.Combine(Newline.Span));
    }

    public override bool Equals(object? obj) => obj is CsvDialect<T> other && Equals(other);
    public static bool operator ==(CsvDialect<T> left, CsvDialect<T> right) => left.Equals(right);
    public static bool operator !=(CsvDialect<T> left, CsvDialect<T> right) => !(left == right);
}
