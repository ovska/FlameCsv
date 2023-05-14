using FlameCsv.Extensions;

namespace FlameCsv.Converters;

/// <summary>
/// Converts instances of <see cref="Nullable{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value and the type parameter of <see cref="Nullable{T}"/></typeparam>
internal sealed class NullableConverter<T, TValue> : CsvConverter<T, TValue?>
    where T : unmanaged, IEquatable<T>
    where TValue : struct
{
    private readonly CsvConverter<T, TValue> _converter;
    private readonly ReadOnlyMemory<T> _null;

    /// <inheritdoc cref="NullableConverter{T,TValue}"/>
    /// <param name="inner">Converter for possible non-null values.</param>
    /// <param name="nullToken">Tokens that match a null value. Default is empty.</param>
    public NullableConverter(
        CsvConverter<T, TValue> inner,
        ReadOnlyMemory<T> nullToken = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _converter = inner;
        _null = nullToken;
    }

    public override bool CanConvert(Type resultType) => Nullable.GetUnderlyingType(resultType) == typeof(TValue);

    public override bool TryParse(ReadOnlySpan<T> span, out TValue? value)
    {
        if (_converter.TryParse(span, out var _value))
        {
            value = _value;
            return true;
        }

        value = default;
        return _null.Span.SequenceEqual(span);
    }

    public override bool TryFormat(Span<T> buffer, TValue? value, out int charsWritten)
    {
        if (value.HasValue)
        {
            return _converter.TryFormat(buffer, value.GetValueOrDefault(), out charsWritten);
        }

        return _null.Span.TryWriteTo(buffer, out charsWritten);
    }
}
