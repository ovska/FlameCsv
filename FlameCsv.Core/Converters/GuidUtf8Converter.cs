using System.Buffers;
using System.Buffers.Text;

namespace FlameCsv.Converters;

internal sealed class GuidUtf8Converter : CsvConverter<byte, Guid>
{
    private readonly StandardFormat _standardFormat;

    public GuidUtf8Converter(StandardFormat standardFormat = default)
    {
        _standardFormat = standardFormat;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out Guid value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat.Symbol) && bytesConsumed == source.Length;
    }
    public override bool TryFormat(Span<byte> destination, Guid value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }
}
