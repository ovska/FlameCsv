using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace FlameCsv.Converters;

internal sealed class SpanUtf8ParsableConverter<TValue> : CsvConverter<byte, TValue>
    where TValue : ISpanFormattable, IUtf8SpanParsable<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    public SpanUtf8ParsableConverter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            provider: _provider,
            shouldAppend: out bool shouldAppend);

        if (shouldAppend)
        {
            handler.AppendFormatted(value, _format);
            return Utf8.TryWrite(destination, ref handler, out charsWritten);
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _provider, out value);
    }
}
