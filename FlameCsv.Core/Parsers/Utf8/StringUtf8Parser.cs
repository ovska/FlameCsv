using System.Diagnostics;
using System.Text;

namespace FlameCsv.Parsers.Utf8;

public sealed class StringUtf8Parser :
    ICsvParser<byte, string>,
    ICsvParser<byte, char[]>,
    ICsvParser<byte, Memory<char>>,
    ICsvParser<byte, ReadOnlyMemory<char>>,
    ICsvParserFactory<byte>
{
    public bool TryParse(ReadOnlySpan<byte> span, out string value)
    {
        if (!span.IsEmpty)
        {
            int maxLength = Encoding.UTF8.GetMaxCharCount(span.Length);

            if (Token<byte>.CanStackalloc(maxLength))
            {
                Span<char > charSpan = stackalloc char[maxLength];
                int charCount = Encoding.UTF8.GetChars(span, charSpan);
                value = new string(charSpan.Slice(0, charCount));
            }
            else
            {
                value = Encoding.UTF8.GetString(span);
            }
        }
        else
        {
            value = "";
        }

        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out char[] value)
    {
        if (span.IsEmpty)
        {
            value = Array.Empty<char>();
            return true;
        }

        var charCount = Encoding.UTF8.GetCharCount(span);
        value = new char[charCount];

        if (charCount != Encoding.UTF8.GetChars(span, value))
            Debug.Fail("Failed to properly UTF8 decode");

        return true;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out Memory<char> value)
    {
        bool result = TryParse(span, out char[] charArray);
        value = charArray;
        return result;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ReadOnlyMemory<char> value)
    {
        bool result = TryParse(span, out char[] charArray);
        value = charArray;
        return result;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(string)
            || resultType == typeof(char[])
            || resultType == typeof(Memory<char>)
            || resultType == typeof(ReadOnlyMemory<char>);
    }

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        return this;
    }
}
