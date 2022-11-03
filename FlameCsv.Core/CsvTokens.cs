using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv;

/// <summary>
/// Contains the configuration of CSV structural tokens.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public readonly partial record struct CsvTokens<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Column delimiter token.
    /// </summary>
    public T Delimiter { get; init; }

    /// <summary>
    /// String delimiter, between which all other tokens can appear as-is.
    /// When used in a string, must be preceded with another string delimiter.
    /// </summary>
    public T StringDelimiter { get; init; }

    /// <summary>
    /// Newline tokens separating CSV rows. Must not be empty.
    /// </summary>
    public ReadOnlyMemory<T> NewLine { get; init; }

    /// <summary>
    /// Whitespace tokens to trim when parsing columns. Set to empty to process all columns as-is.
    /// </summary>
    public ReadOnlyMemory<T> Whitespace { get; init; }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="other"/> is structurally equal to the current instance.
    /// </summary>
    public bool Equals(CsvTokens<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && StringDelimiter.Equals(other.StringDelimiter)
            && NewLine.Span.SequenceEqual(other.NewLine.Span)
            && Whitespace.Span.SequenceEqual(other.Whitespace.Span);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            StringDelimiter,
            NewLine.IsEmpty ? 0 : HashCode<T>.Combine(NewLine.Span),
            Whitespace.IsEmpty ? 0 : HashCode<T>.Combine(Whitespace.Span));
    }
}
