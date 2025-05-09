using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Formattable;

internal sealed class SpanUtf8FormattableConverter<TValue> : CsvConverter<byte, TValue>
    where TValue : IUtf8SpanFormattable, ISpanParsable<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    public SpanUtf8FormattableConverter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return ReadExtensions.TryParseFromUtf8(source, _provider, out value);
    }
}
