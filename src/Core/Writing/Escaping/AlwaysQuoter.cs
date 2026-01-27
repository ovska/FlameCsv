namespace FlameCsv.Writing.Escaping;

internal sealed class AlwaysQuoter<T>(T quote) : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        return new QuotingResult(true, field.Count(quote));
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        return new QuotingResult(true, field.Count((char)ushort.CreateTruncating(quote)));
    }

    public T Quote => quote;
}
