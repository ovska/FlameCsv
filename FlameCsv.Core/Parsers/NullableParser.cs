namespace FlameCsv.Parsers;

/// <summary>
/// Parses instances of <see cref="Nullable{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value and the type parameter of <see cref="Nullable{T}"/></typeparam>
public sealed class NullableParser<T, TValue> :
    ICsvParser<T, TValue?>
    where TValue : struct
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Parser for possible non-null values.
    /// </summary>
    public ICsvParser<T, TValue> Inner { get; }

    /// <summary>
    /// Tokens that match a null value. Default is empty.
    /// </summary>
    public ReadOnlyMemory<T> NullToken { get; }

    /// <inheritdoc cref="NullableParser{T,TValue}"/>
    /// <param name="inner">Parser for possible non-null values.</param>
    /// <param name="nullToken">Tokens that match a null value. Default is empty.</param>
    public NullableParser(
        ICsvParser<T, TValue> inner,
        ReadOnlyMemory<T> nullToken = default)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
        NullToken = nullToken;
    }

    public bool TryParse(ReadOnlySpan<T> span, out TValue? value)
    {
        if (Inner.TryParse(span, out var _value))
        {
            value = _value;
            return true;
        }

        value = default;
        return NullToken.Span.SequenceEqual(span);
    }

    public bool CanParse(Type resultType) => Nullable.GetUnderlyingType(resultType) == typeof(TValue);
}
