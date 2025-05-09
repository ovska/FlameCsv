using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Converters.Formattable;

internal sealed class NumberUtf8Converter<TValue> : CsvConverter<byte, TValue>
    where TValue : INumberBase<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;
    private readonly NumberStyles _styles;

    public NumberUtf8Converter(CsvOptions<byte> options, NumberStyles defaultStyles)
    {
        ArgumentNullException.ThrowIfNull(options);

        Debug.Assert(
            defaultStyles switch
            {
                NumberStyles.Integer => typeof(TValue).IsAssignableTo(
                    typeof(IBinaryInteger<>).MakeGenericType(typeof(TValue))
                ),
                NumberStyles.Float => typeof(TValue).IsAssignableTo(
                    typeof(IFloatingPoint<>).MakeGenericType(typeof(TValue))
                ),
                _ => false,
            },
            $"Unexpected default styles {defaultStyles} for {typeof(TValue)}"
        );

        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
        _styles = options.GetNumberStyles(typeof(TValue), defaultStyles);
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int bytesWritten)
    {
        return value.TryFormat(destination, out bytesWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        return TValue.TryParse(source, _styles, _provider, out value);
    }
}
