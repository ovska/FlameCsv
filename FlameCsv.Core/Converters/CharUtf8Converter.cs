using System.Text;

namespace FlameCsv.Converters;

internal sealed class CharUtf8Converter : CsvConverter<byte, char>
{
    public static CharUtf8Converter Instance { get; } = new();

    public override bool TryParse(ReadOnlySpan<byte> source, out char value)
    {
        if (source.Length == 1 && source[0] < 128)
        {
            value = (char)source[0];
            return true;
        }

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
        if (value < 128)
        {
            destination[0] = (byte)value;
            charsWritten = 1;
            return true;
        }

        if (Encoding.UTF8.TryGetBytes([value], destination, out int written))
        {
            charsWritten = written;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}
