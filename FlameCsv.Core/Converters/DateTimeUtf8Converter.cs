using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class DateTimeUtf8Converter : CsvConverter<byte, DateTime>
{
    private readonly StandardFormat _standardFormat;

    public DateTimeUtf8Converter(StandardFormat standardFormat)
    {
        _standardFormat = standardFormat;
    }

    public override bool TryFormat(Span<byte> destination, DateTime value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out DateTime value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat.Symbol) && bytesConsumed == source.Length;
    }
}
