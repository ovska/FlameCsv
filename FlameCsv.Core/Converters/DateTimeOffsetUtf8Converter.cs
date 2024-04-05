using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class DateTimeOffsetUtf8Converter : CsvConverter<byte, DateTimeOffset>
{
    private readonly StandardFormat _standardFormat;

    public DateTimeOffsetUtf8Converter(StandardFormat standardFormat)
    {
        _standardFormat = standardFormat;
    }

    public override bool TryFormat(Span<byte> destination, DateTimeOffset value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out DateTimeOffset value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat.Symbol) && bytesConsumed == source.Length;
    }
}
