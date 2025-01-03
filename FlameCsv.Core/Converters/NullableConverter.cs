namespace FlameCsv.Converters;

/// <summary>
/// Converts instances of <see cref="Nullable{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value and the type parameter of <see cref="Nullable{T}"/></typeparam>
public sealed class NullableConverter<T, TValue> : CsvConverter<T, TValue?>
    where T : unmanaged, IEquatable<T>
    where TValue : struct
{
    public override bool HandleNull => true;

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

    /// <inheritdoc cref="NullableConverter{T, TValue}"/>
    public NullableConverter(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _converter = options.GetConverter<TValue>();
        _null = options.GetNullToken(typeof(TValue));
    }

    public override bool TryParse(ReadOnlySpan<T> source, out TValue? value)
    {
        if (_converter.TryParse(source, out TValue v))
        {
            value = v;
            return true;
        }

        value = default;
        return _null.Span.SequenceEqual(source);
    }

    public override bool TryFormat(Span<T> destination, TValue? value, out int charsWritten)
    {
        if (value.HasValue)
        {
            ref readonly TValue v = ref Nullable.GetValueRefOrDefaultRef(in value);
            return _converter.TryFormat(destination, v, out charsWritten);
        }

        if (_null.Length <= destination.Length)
        {
            _null.Span.CopyTo(destination);
            charsWritten = _null.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}
