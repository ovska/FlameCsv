using System.Buffers.Text;

namespace FlameCsv.Converters;

internal sealed class GuidUtf8Converter : CsvConverter<byte, Guid>
{
    private readonly char _standardFormat;

    public GuidUtf8Converter(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out Guid _, out _, standardFormat);

        _standardFormat = standardFormat;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out Guid value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat) && bytesConsumed == source.Length;
    }
    public override bool TryFormat(Span<byte> destination, Guid value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }
}
