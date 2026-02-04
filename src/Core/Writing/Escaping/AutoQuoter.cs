using System.Buffers;

namespace FlameCsv.Writing.Escaping;

internal sealed class AutoQuoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public T Quote { get; }

    private readonly SearchValues<T> _needsQuoting;
    private readonly SearchValues<char> _needsQuotingChar;

    public AutoQuoter(CsvOptions<T> options)
    {
        Check.NotNull(options.Quote, "Quote must be set for AutoQuoter.");
        Quote = T.CreateTruncating(options.Quote.GetValueOrDefault());
        _needsQuoting = options.NeedsQuoting;
        _needsQuotingChar = options.NeedsQuotingChar;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        int index = field.IndexOfAny(_needsQuoting);
        return index >= 0 ? new QuotingResult(true, field.Slice(index).Count(Quote)) : default;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        int index = field.IndexOfAny(_needsQuotingChar);
        return index >= 0
            ? new QuotingResult(true, field.Slice(index).Count((char)ushort.CreateTruncating(Quote)))
            : default;
    }
}
