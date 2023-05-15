using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class DateTimeOffsetUtf8Converter : CsvConverter<byte, DateTimeOffset>
{
    private readonly char _standardFormat;

    public DateTimeOffsetUtf8Converter(CsvUtf8Options options)
    {
        _standardFormat = options.DateTimeFormat;
    }

    public override bool TryFormat(Span<byte> destination, DateTimeOffset value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out DateTimeOffset value)
    {
        return Utf8Parser.TryParse(source, out value, out int bytesConsumed, _standardFormat) && bytesConsumed == source.Length;
    }
}
