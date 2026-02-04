using System.Buffers;

namespace FlameCsv.Writing.Escaping;

internal sealed class Quoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly SearchValues<T> _needsQuoting;
    private readonly SearchValues<char> _needsQuotingChar;
    private readonly bool _empty;
    private readonly bool _auto;
    private readonly bool _leading;
    private readonly bool _trailing;

    public Quoter(CsvOptions<T> options)
    {
        Check.NotNull(options.Quote, "Quote must be set for Quoter.");
        _needsQuoting = options.NeedsQuoting;
        _needsQuotingChar = options.NeedsQuotingChar;
        Quote = T.CreateTruncating(options.Quote.GetValueOrDefault());
        _empty = (options.FieldQuoting & CsvFieldQuoting.Empty) != 0;
        _auto = (options.FieldQuoting & CsvFieldQuoting.Auto) != 0;
        _leading = (options.FieldQuoting & CsvFieldQuoting.LeadingSpaces) != 0;
        _trailing = (options.FieldQuoting & CsvFieldQuoting.TrailingSpaces) != 0;
    }

    public T Quote { get; }

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        if (field.IsEmpty)
        {
            return new QuotingResult(_empty, 0);
        }

        bool retVal = false;

        // range for the count scan
        int start = 0;
        int length = field.Length;

        if (_leading && field[0] == T.CreateTruncating(' '))
        {
            retVal = true;
            start++;
            length--;
        }

        if (_trailing && !retVal && field[^1] == T.CreateTruncating(' '))
        {
            retVal = true;
            length--;
        }

        if (_auto && !retVal)
        {
            int index = field.IndexOfAny(_needsQuoting);

            if (index >= 0)
            {
                retVal = true;
                start = index;
                length = field.Length - index;
            }
        }

        if (retVal)
        {
            int quoteCount = field.Slice(start, length).Count(Quote);
            return new QuotingResult(true, quoteCount);
        }

        return default;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        if (field.IsEmpty)
        {
            return new QuotingResult(_empty, 0);
        }

        bool retVal = false;

        // range for the count scan
        int start = 0;
        int length = field.Length;

        if (_leading && field[0] == ' ')
        {
            retVal = true;
            start++;
            length--;
        }

        if (_trailing && !retVal && field[^1] == ' ')
        {
            retVal = true;
            length--;
        }

        if (_auto && !retVal)
        {
            int index = field.IndexOfAny(_needsQuotingChar);

            if (index >= 0)
            {
                retVal = true;
                start = index;
                length = field.Length - index;
            }
        }

        if (retVal)
        {
            int quoteCount = field.Slice(start, length).Count((char)ushort.CreateTruncating(Quote));
            return new QuotingResult(true, quoteCount);
        }

        return default;
    }
}
