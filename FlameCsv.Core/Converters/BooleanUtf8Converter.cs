using System.Buffers;
using System.Buffers.Text;

namespace FlameCsv.Converters;

internal sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    private readonly StandardFormat _standardFormat;

    public BooleanUtf8Converter(StandardFormat standardFormat = default)
    {
        _standardFormat = standardFormat;
    }

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat.Symbol)
            && bytesConsumed == source.Length;
    }
}
