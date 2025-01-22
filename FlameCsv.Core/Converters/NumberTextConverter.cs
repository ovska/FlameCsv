using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters;

internal sealed class NumberTextConverter<TValue, TStyles> : CsvConverter<char, TValue>
    where TValue : INumberBase<TValue>
    where TStyles : INumberStylesDefaultValue
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;
    private readonly NumberStyles _styles;

    public NumberTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
        _styles = options.GetNumberStyles(typeof(TValue), TStyles.Default);
    }

    public override bool TryFormat(Span<char> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _styles, _provider, out value);
    }
}
