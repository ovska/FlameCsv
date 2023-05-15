using System.Diagnostics.CodeAnalysis;

namespace FlameCsv;

public abstract class CsvConverter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter, or a suitable converter can be
    /// created if this is a factory instance.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><see langword="true"/> if the converter is suitable for <paramref name="type"/></returns>
    public abstract bool CanConvert(Type type);
}

public abstract class CsvConverter<T, TValue> : CsvConverter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><see langword="true"/> if the converter is suitable for <paramref name="type"/></returns>
    public sealed override bool CanConvert(Type type) => type == typeof(TValue);

    /// <summary>
    /// Attempts to parse <paramref name="value"/> from the field.
    /// </summary>
    /// <param name="source">CSV field</param>
    /// <param name="value">Parsed value</param>
    /// <returns><see langword="true"/> if the value was successfully parsed.</returns>
    public abstract bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to format <paramref name="value"/> into the field.
    /// </summary>
    /// <param name="destination">Buffer to format the value to</param>
    /// <param name="value">Value to format</param>
    /// <param name="charsWritten">If successful, how many characters were written to <paramref name="destination"/></param>
    /// <returns><see langword="true"/> if the value was successfully formatted.</returns>
    public abstract bool TryFormat(Span<T> destination, TValue value, out int charsWritten);

    /// <summary>
    /// Whether the converter formats null values.
    /// </summary>
    protected internal virtual bool HandleNull => false;
}

public abstract class CsvConverterFactory<T> : CsvConverter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Creates an instance capable of converting values of the specified type.
    /// </summary>
    /// <remarks>
    /// This method should only be called after <see cref="CsvConverter{T}.CanParse"/> has verified the type is valid.
    /// </remarks>
    /// <param name="type">Value type of the returned <see cref="CsvConverter{T,TValue}"/></param>
    /// <param name="options">Current options instance</param>
    /// <returns>Parser instance</returns>
    public abstract CsvConverter<T> Create(Type type, CsvOptions<T> options);

    public virtual CsvConverter<T, TValue> Create<TValue>(CsvOptions<T> options)
    {
        return (CsvConverter<T, TValue>)Create(typeof(TValue), options);
    }
}
