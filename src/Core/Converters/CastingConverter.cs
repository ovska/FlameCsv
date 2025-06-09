using JetBrains.Annotations;

namespace FlameCsv.Converters;

internal sealed class CastingConverter<T, TIn, TOut> : CsvConverter<T, TOut>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvConverter<T, TIn> _inConverter;
    private readonly Func<TIn, TOut> _convertTo;
    private readonly Func<TOut, TIn> _convertFrom;

    internal CastingConverter(
        CsvConverter<T, TIn> inConverter,
        [RequireStaticDelegate] Func<TIn, TOut> convertTo,
        [RequireStaticDelegate] Func<TOut, TIn> convertFrom
    )
    {
        ArgumentNullException.ThrowIfNull(inConverter);
        ArgumentNullException.ThrowIfNull(convertTo);
        ArgumentNullException.ThrowIfNull(convertFrom);
        _inConverter = inConverter;
        _convertTo = convertTo;
        _convertFrom = convertFrom;
    }

    public override bool TryParse(ReadOnlySpan<T> source, out TOut value)
    {
        if (_inConverter.TryParse(source, out var inValue))
        {
            value = _convertTo(inValue);
            return true;
        }

        value = default!;
        return false;
    }

    public override bool TryFormat(Span<T> destination, TOut value, out int charsWritten)
    {
        return _inConverter.TryFormat(destination, _convertFrom(value), out charsWritten);
    }
}
