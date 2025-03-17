using System.Text;

namespace FlameCsv.Converters;

internal sealed class CharUtf8Converter : CsvConverter<byte, char>
{
    public static CharUtf8Converter Instance { get; } = new();

    public override bool TryParse(ReadOnlySpan<byte> source, out char value)
    {
        Span<char> chars = stackalloc char[8];

        if (Encoding.UTF8.TryGetChars(source, chars, out int charsWritten) &&
            charsWritten == 1)
        {
            value = chars[0];
            return true;
        }

        value = '\0';
        return false;
    }

    public override bool TryFormat(Span<byte> destination, char value, out int charsWritten)
    {
        if (Encoding.UTF8.TryGetBytes([value], destination, out int written))
        {
            charsWritten = written;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}
