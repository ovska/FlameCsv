using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

public sealed class StringUtf8Converter : CsvConverter<byte, string>
{
    public override bool TryFormat(Span<byte> buffer, string value, out int charsWritten)
    {
        return value.AsSpan().TryWriteUtf8To(buffer, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<byte> span, out string value)
    {
        if (!span.IsEmpty)
        {
            int maxLength = Encoding.UTF8.GetMaxCharCount(span.Length);

            if (Token<byte>.CanStackalloc(maxLength))
            {
                Span<char> charSpan = stackalloc char[maxLength];
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
}
