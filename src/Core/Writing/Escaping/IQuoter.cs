namespace FlameCsv.Writing.Escaping;

internal record struct QuotingResult(bool NeedsQuoting, int SpecialCount);

internal interface IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    T Quote { get; }
    QuotingResult NeedsQuoting(ReadOnlySpan<T> field);
    QuotingResult NeedsQuoting(ReadOnlySpan<char> field);
}
