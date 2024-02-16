using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class StringUtf8Converter : CsvConverter<byte, string>
{
    public override bool HandleNull => true;

    public static StringUtf8Converter Instance { get; } = new();

    public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
    {
        return value.AsSpan().TryWriteUtf8To(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out string value)
    {
        if (!source.IsEmpty)
        {
            int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

            // If possible, traverse the bytes only once
            if (Token<byte>.CanStackalloc(maxLength))
            {
                Span<char> charSpan = stackalloc char[maxLength];
                int charCount = Encoding.UTF8.GetChars(source, charSpan);
                value = new string(charSpan.Slice(0, charCount));
            }
            else
            {
                value = Encoding.UTF8.GetString(source);
            }
        }
        else
        {
            value = "";
        }

        return true;
    }
}
