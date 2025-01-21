namespace FlameCsv.Converters;

public abstract class NullableConverterBase<T, TValue> : CsvConverter<T, TValue?>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    /// <inheritdoc />
    protected internal sealed override bool CanFormatNull => true;

    private readonly CsvConverter<T, TValue> _converter;

    protected abstract ReadOnlySpan<T> Null { get; }

    protected NullableConverterBase(CsvConverter<T, TValue> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
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

        var @null = Null;
        return @null.IsEmpty && source.IsEmpty || @null.SequenceEqual(source);
    }

    /// <inheritdoc />
    public override bool TryFormat(Span<T> destination, TValue? value, out int charsWritten)
    {
        if (value.HasValue)
        {
            ref readonly TValue v = ref Nullable.GetValueRefOrDefaultRef(in value);
            return _converter.TryFormat(destination, v, out charsWritten);
        }

        var @null = Null;

        if (@null.Length <= destination.Length)
        {
            if (!@null.IsEmpty) @null.CopyTo(destination);
            charsWritten = @null.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}

/// <summary>
/// Converts instances of <see cref="Nullable{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value and the type parameter of <see cref="Nullable{T}"/></typeparam>
public sealed class NullableConverter<T, TValue> : NullableConverterBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    private readonly ReadOnlyMemory<T> _null;

    /// <inheritdoc cref="NullableConverter{T,TValue}"/>
    /// <param name="inner">Converter for possible non-null values.</param>
    /// <param name="nullToken">Tokens that match a null value. Default is empty.</param>
    public NullableConverter(
        CsvConverter<T, TValue> inner,
        ReadOnlyMemory<T> nullToken = default)
        : base(inner)
    {
        _null = nullToken;
    }

    protected override ReadOnlySpan<T> Null => _null.Span;
}

internal sealed class OptimizedNullEmptyConverter<T, TValue>(CsvConverter<T, TValue> converter)
    : NullableConverterBase<T, TValue>(converter)
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    protected override ReadOnlySpan<T> Null => ReadOnlySpan<T>.Empty;
}

internal sealed class OptimizedNullStringConverter<TValue> : NullableConverterBase<char, TValue>
    where TValue : struct
{
    private readonly string? _string;
    private readonly int _start;
    private readonly int _length;

    public OptimizedNullStringConverter(CsvConverter<char, TValue> converter, string? value, int start, int length)
        : base(converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _string = value;
        _start = start;
        _length = length;
    }

    protected override ReadOnlySpan<char> Null => _string.AsSpan(_start, _length);
}

internal sealed class OptimizedNullArrayConverter<T, TValue> : NullableConverterBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    private readonly ArraySegment<T> _array;

    public OptimizedNullArrayConverter(CsvConverter<T, TValue> converter, ArraySegment<T> array)
        : base(converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _array = array;
    }

    protected override ReadOnlySpan<T> Null => _array.AsSpan();
}
