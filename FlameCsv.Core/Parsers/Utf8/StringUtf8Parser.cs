using System.Text;

namespace FlameCsv.Parsers.Utf8;

public sealed class StringUtf8Parser :
    ICsvParser<byte, string>,
    ICsvParser<byte, char[]>,
    ICsvParser<byte, Memory<char>>,
    ICsvParser<byte, ReadOnlyMemory<char>>
{
    internal static readonly StringUtf8Parser Instance = new();

    public bool TryParse(ReadOnlySpan<byte> span, out string value)
    {
        value = !span.IsEmpty ? Encoding.UTF8.GetString(span) : "";
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
        return charCount == Encoding.UTF8.GetChars(span, value); // should always succeed
    }

    public bool TryParse(ReadOnlySpan<byte> span, out Memory<char> value)
    {
        if (TryParse(span, out char[] charArray))
        {
            value = charArray;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ReadOnlyMemory<char> value)
    {
        value = !span.IsEmpty ? Encoding.UTF8.GetString(span).AsMemory() : default;
        return true;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(string)
            || resultType == typeof(char[])
            || resultType == typeof(Memory<char>)
            || resultType == typeof(ReadOnlyMemory<char>);
    }
}
