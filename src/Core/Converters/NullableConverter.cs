namespace FlameCsv.Converters;

/// <summary>
/// Base class for converters that handle <see cref="Nullable{T}"/>.
/// </summary>
internal sealed class NullableConverter<T, TValue> : CsvConverter<T, TValue?>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    /// <inheritdoc />
    protected internal override bool CanFormatNull => true;

    private readonly CsvConverter<T, TValue> _converter;
    private readonly ReadOnlyMemory<T> _null;

    /// <summary>
    /// Creates a new instance wrapping the converter.
    /// </summary>
    /// <param name="converter">Converter to convert non-null values</param>
    /// <param name="nullToken"></param>
    public NullableConverter(CsvConverter<T, TValue> converter, ReadOnlyMemory<T> nullToken)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
        _null = nullToken;
    }

    /// <inheritdoc />
    public override bool TryParse(ReadOnlySpan<T> source, out TValue? value)
    {
        if (_converter.TryParse(source, out TValue v))
        {
            value = v;
            return true;
        }

        value = null;

        return _null.IsEmpty && source.IsEmpty || _null.Span.SequenceEqual(source);
    }

    /// <inheritdoc />
    public override bool TryFormat(Span<T> destination, TValue? value, out int charsWritten)
    {
        if (value.HasValue)
        {
            ref readonly TValue v = ref Nullable.GetValueRefOrDefaultRef(in value);
            return _converter.TryFormat(destination, v, out charsWritten);
        }

        if (_null.IsEmpty)
        {
            charsWritten = 0;
            return true;
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
