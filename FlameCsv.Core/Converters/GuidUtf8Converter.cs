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

    public override bool TryParse(ReadOnlySpan<byte> span, out Guid value)
    {
        return Utf8Parser.TryParse(span, out value, out int bytesConsumed, _standardFormat) && bytesConsumed == span.Length;
    }
    public override bool TryFormat(Span<byte> buffer, Guid value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, buffer, out charsWritten, _standardFormat);
    }
}
