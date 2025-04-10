using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

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
        return ReadExtensions.TryFormatToUtf8(destination, value, _format, _provider, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _provider, out value);
    }
}
