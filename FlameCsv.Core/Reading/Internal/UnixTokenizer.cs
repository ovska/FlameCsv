using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal class UnixTokenizer<T> : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _escape;
    private readonly T _newlineFirst;
    private readonly T _newlineSecond;
    private readonly int _newlineLength;

    public UnixTokenizer(CsvOptions<T> options)
    {
        _delimiter = T.CreateTruncating(options.Delimiter);
        _quote = T.CreateTruncating(options.Quote);
        _escape = T.CreateTruncating(options.Escape!.Value);
        _newlineLength = options.Newline.GetTokens(out _newlineFirst, out _newlineSecond);
    }

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

            if (current == _delimiter || current == _newlineFirst || current == _newlineSecond)
            {
                bool isEOL = current != _delimiter;
                int newlineLength = _newlineLength;

                if (isEOL && newlineLength == 2)
                {
                    if (index + 1 < data.Length && current == _newlineFirst && data[index + 1] == _newlineSecond)
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
            ThrowHelper.ThrowLeftInEscape();
        }

        if (readToEnd && index == data.Length && metaIndex < metaBuffer.Length)
        {
            metaBuffer[metaIndex++] = Meta.Unix(
                data.Length,
                quotesConsumed,
                escapesConsumed,
                isEOL: true,
                newlineLength: 0
            );
        }

        return metaIndex;
    }
}

file static class ThrowHelper
{
    public static void ThrowLeftInEscape()
    {
        throw new CsvFormatException("The final field ended in an escape token.");
    }
}
