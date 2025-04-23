using FlameCsv.Exceptions;

namespace FlameCsv.Reading.Internal;

internal class UnixTokenizer<T>(ref readonly CsvDialect<T> dialect) : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly T _delimiter = dialect.Delimiter;
    private readonly T _quote = dialect.Quote;
    private readonly T _escape = dialect.Escape!.Value;
    private readonly NewlineBuffer<T> _newline = dialect.Newline;

    public override int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex, bool readToEnd)
    {
        int metaIndex = 0;
        int index = startIndex;

        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        bool inEscape = false;

        while (index < data.Length && metaIndex < metaBuffer.Length)
        {
            if (inEscape)
            {
                inEscape = false;
                goto Continue;
            }

            T current = data[index];

            if (current == _escape)
            {
                inEscape = true;
                escapesConsumed++;
                goto Continue;
            }

            if (current == _quote)
            {
                quotesConsumed++;
                goto Continue;
            }

            if (quotesConsumed % 2 != 0)
            {
                goto Continue;
            }

            if (current == _delimiter || current == _newline.First || current == _newline.Second)
            {
                bool isEOL = current != _delimiter;
                int newlineLength = _newline.Length;

                if (isEOL && newlineLength == 2)
                {
                    if (index + 1 < data.Length &&
                        current == _newline.First &&
                        data[index + 1] == _newline.Second)
                    {
                        newlineLength = 2;
                    }
                    else
                    {
                        newlineLength = 1;
                    }
                }

                Meta meta = Meta.Unix(index, quotesConsumed, escapesConsumed, isEOL, newlineLength);
                metaBuffer[metaIndex++] = meta;
                quotesConsumed = 0;
                escapesConsumed = 0;
                index += meta.EndOffset;
                continue;
            }

        Continue:
            index++;
        }

        if (readToEnd && inEscape)
        {
            ThrowLeftInEscape();
        }

        if (readToEnd && index == data.Length && metaIndex < metaBuffer.Length)
        {
            metaBuffer[metaIndex++] = Meta.Unix(
                data.Length,
                quotesConsumed,
                escapesConsumed,
                isEOL: true,
                newlineLength: 0);
        }

        return metaIndex;
    }

    private static void ThrowLeftInEscape()
    {
        throw new CsvFormatException("The final field ended in an escape token.");
    }
}
