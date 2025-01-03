using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace FlameCsv.Converters;

internal sealed class IntegerNumberTextConverter<TValue>(CsvOptions<char> options) : BaseNumberTextConverter<TValue>(options)
    where TValue : IBinaryInteger<TValue>
{
    protected override NumberStyles Styles { get; }= options.GetNumberStyles(typeof(TValue), NumberStyles.Integer);
}

internal sealed class FloatNumberTextConverter<TValue>(CsvOptions<char> options) : BaseNumberTextConverter<TValue>(options)
    where TValue : IFloatingPoint<TValue>
{
    protected override NumberStyles Styles { get; } = options.GetNumberStyles(typeof(TValue), NumberStyles.Float);
}

internal abstract class BaseNumberTextConverter<TValue> : CsvConverter<char, TValue> where TValue : INumberBase<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    protected BaseNumberTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    protected abstract NumberStyles Styles { get; }

    public override bool TryFormat(Span<char> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, Styles, _provider, out value);
    }
}
