using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv;

public readonly partial record struct CsvParserOptions<T> where T : unmanaged, IEquatable<T>
{
    public bool Equals(CsvParserOptions<T> other)
    {
        return Delimiter.Equals(other.Delimiter)
            && StringDelimiter.Equals(other.StringDelimiter)
            && NewLine.Span.SequenceEqual(other.NewLine.Span)
            && Whitespace.Span.SequenceEqual(other.Whitespace.Span);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Delimiter,
            StringDelimiter,
            NewLine.IsEmpty ? 0 : HashCode<T>.Combine(NewLine.Span),
            Whitespace.IsEmpty ? 0 : HashCode<T>.Combine(Whitespace.Span));
    }
}
