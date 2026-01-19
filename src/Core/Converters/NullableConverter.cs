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

    internal readonly CsvConverter<T, TValue> _converter;
    private readonly Utf8String _null;

    /// <summary>
    /// Creates a new instance wrapping the converter.
    /// </summary>
    /// <param name="converter">Converter to convert non-null values</param>
    /// <param name="nullToken"></param>
    public NullableConverter(CsvConverter<T, TValue> converter, Utf8String? nullToken)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
        _null = nullToken ?? Utf8String.Empty;
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
        ReadOnlySpan<T> nullSpan = _null.AsSpan<T>();
        return (nullSpan.IsEmpty && source.IsEmpty) || nullSpan.SequenceEqual(source);
    }

    /// <inheritdoc />
    public override bool TryFormat(Span<T> destination, TValue? value, out int charsWritten)
    {
        if (value.HasValue)
        {
            ref readonly TValue v = ref Nullable.GetValueRefOrDefaultRef(in value);
            return _converter.TryFormat(destination, v, out charsWritten);
        }

        ReadOnlySpan<T> nullSpan = _null.AsSpan<T>();
        charsWritten = nullSpan.Length;
        return nullSpan.IsEmpty || nullSpan.TryCopyTo(destination);
    }
}
