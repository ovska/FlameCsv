using System.Text;

namespace FlameCsv.Converters;

internal sealed class StringUtf8Converter : CsvConverter<byte, string>
{
    public static StringUtf8Converter Instance { get; } = new();

    public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
    {
        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out string value)
    {
        value = Encoding.UTF8.GetString(source);
        return true;
    }
}
