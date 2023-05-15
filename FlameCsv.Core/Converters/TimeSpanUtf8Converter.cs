using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class TimeSpanUtf8Converter : CsvConverter<byte, TimeSpan>
{
    private readonly char _standardFormat;

    public TimeSpanUtf8Converter(CsvUtf8Options options)
    {
        _standardFormat = options.TimeSpanFormat;
    }

    public override bool TryFormat(Span<byte> destination, TimeSpan value, out int charsWritten)
    {
        return Utf8Formatter.TryFormat(value, destination, out charsWritten, _standardFormat);
    }

    public override bool TryParse(ReadOnlySpan<byte> readOnlySpan, [MaybeNullWhen(false)] out TimeSpan value)
    {
        return Utf8Parser.TryParse(readOnlySpan, out value, out int bytesConsumed, _standardFormat)
            && bytesConsumed == readOnlySpan.Length;
    }
}
