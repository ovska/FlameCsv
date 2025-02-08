using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

/// <summary>
/// Base class for converters that handle <see cref="Nullable{T}"/>.
/// </summary>
public abstract class NullableConverterBase<T, TValue> : CsvConverter<T, TValue?>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    /// <inheritdoc />
    protected internal sealed override bool CanFormatNull => true;

    private readonly CsvConverter<T, TValue> _converter;

    /// <summary>
    /// Returns the sequence that represents a null value for <typeparamref name="TValue"/>.
    /// </summary>
    protected abstract ReadOnlySpan<T> Null { get; }

    /// <summary>
    /// Creates a new instance wrapping the converter.
    /// </summary>
    /// <param name="converter">Converter to convert non-null values</param>
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

    /// <summary>
    /// Sequence used to represent a null value.
    /// </summary>
    /// <seealso cref="CsvOptions{T}.GetNullToken"/>
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

[InlineArray(length: MaxLength)]
internal struct Container<T>
{
    public const int MaxLength = 8;
    public T elem0;
}

internal sealed class OptimizedKnownLengthConverter<T, TValue> : NullableConverterBase<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
    where TValue : struct
{
    private readonly int _length;
    private Container<T> _container;

    public OptimizedKnownLengthConverter(CsvConverter<T, TValue> converter, ReadOnlyMemory<T> value)
        : base(converter)
    {
        ArgumentNullException.ThrowIfNull(converter);

        _container = default;
        value.Span.CopyTo(_container);
        _length = value.Length;
    }

    protected override ReadOnlySpan<T> Null => MemoryMarshal.CreateReadOnlySpan(ref _container.elem0, _length);
}
