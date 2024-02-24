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
        value = !source.IsEmpty ? Encoding.UTF8.GetString(source) : "";
        return true;
    }
}
