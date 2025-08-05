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

    public override bool Tokenize(RecordBuffer recordBuffer, ReadOnlySpan<T> data, bool readToEnd)
    {
        FieldBuffer destination = recordBuffer.GetUnreadBuffer(minimumLength: 0, out int startIndex);

        if (data.IsEmpty || data.Length < startIndex)
        {
            return false;
        }

        Span<uint> fields = destination.Fields;
        Span<byte> flags = destination.Quotes;
        int fieldIndex = 0;

        int index = startIndex;

        uint quotesConsumed = 0;
        uint escapesConsumed = 0;
        bool inEscape = false;

        while (index < data.Length && fieldIndex < fields.Length)
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
                FieldFlag flag = isEOL ? FieldFlag.EOL : FieldFlag.None;

                if (
                    isEOL
                    && newlineLength == 2
                    && index + 1 < data.Length
                    && current == _newlineFirst
                    && data[index + 1] == _newlineSecond
                )
                {
                    flag = FieldFlag.CRLF;
                }

                fields[fieldIndex] = (uint)index | (uint)flag;
                flags[fieldIndex] = GetQuoteFlag(quotesConsumed, escapesConsumed);

                fieldIndex++;
                quotesConsumed = 0;
                escapesConsumed = 0;
                index += (flag == FieldFlag.CRLF) ? 2 : 1;

                continue;
            }

            Continue:
            index++;
        }

        if (readToEnd && inEscape)
        {
            ThrowHelper.ThrowLeftInEscape();
        }

        if (readToEnd && index == data.Length && fieldIndex < fields.Length)
        {
            // 0 length newline
            fields[fieldIndex] = (uint)index | Field.StartOrEnd;
            flags[fieldIndex] = GetQuoteFlag(quotesConsumed, escapesConsumed);
            fieldIndex++;
        }

        recordBuffer.SetFieldsRead(fieldIndex);
        return fieldIndex > 0;
    }

    private static byte GetQuoteFlag(uint quotesConsumed, uint escapesConsumed)
    {
        if (escapesConsumed == 0)
        {
            Field.SaturateTo7Bits(ref quotesConsumed);
            return (byte)quotesConsumed;
        }
        else
        {
            if (quotesConsumed != 2)
            {
                ThrowHelper.ThrowInvalidQuoteCount(quotesConsumed, escapesConsumed);
            }

            Field.SaturateTo7Bits(ref escapesConsumed);
            return (byte)(0x80 | escapesConsumed);
        }
    }
}

file static class ThrowHelper
{
    public static void ThrowLeftInEscape()
    {
        throw new CsvFormatException("The final field ended in an escape token.");
    }

    public static void ThrowInvalidQuoteCount(uint quoteCount, uint escapeCount)
    {
        throw new CsvFormatException(
            $"There must be exactly 2 quotes in escape-style field (had {quoteCount} quotes and {escapeCount} escapes)"
        );
    }
}
