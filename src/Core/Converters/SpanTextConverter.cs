using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

internal sealed class SpanTextConverter<TValue> : CsvConverter<char, TValue>
    where TValue : ISpanFormattable, ISpanParsable<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    public SpanTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    public override bool TryFormat(Span<char> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _provider, out value);
    }
}
