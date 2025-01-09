using System.Diagnostics.CodeAnalysis;

namespace FlameCsv;

/// <summary>
/// Base class used for registering custom converters. User implemented <see cref="CsvConverter{T}"/> instances
/// must be thread safe.
/// </summary>
/// <remarks>
/// Do not inherit this type directly, but either <see cref="CsvConverter{T, TValue}"/>
/// or <see cref="CsvConverterFactory{T}"/>.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public abstract class CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter, or a suitable converter can be
    /// created from this factory instance.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><see langword="true"/> if the converter is suitable for <paramref name="type"/></returns>
    public abstract bool CanConvert(Type type);
}

/// <summary>
/// Parses and formats <typeparamref name="TValue"/> to/from CSV fields.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed/formatted value</typeparam>
public abstract class CsvConverter<T, TValue> : CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
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
    /// Whether the converter formats null values. When <see langword="false"/> (the default),
    /// <see cref="CsvOptions{T}.GetNullToken(Type)"/> is used to write nulls.
    /// </summary>
    protected internal virtual bool CanFormatNull => false;
}

/// <summary>
/// Creates instances of <see cref="CsvConverterFactory{T}"/>.
/// By default, used to resolve converters for <see langword="enum"/> and <see cref="Nullable{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public abstract class CsvConverterFactory<T> : CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Creates an instance capable of converting values of the specified type.
    /// </summary>
    /// <remarks>
    /// This method should only be called after <see cref="CsvConverter{T}.CanConvert(Type)"/> has returned <see langword="true"/>.
    /// </remarks>
    /// <param name="type"><c>TValue</c> of the returned <see cref="CsvConverter{T,TValue}"/></param>
    /// <param name="options">Current options instance</param>
    /// <returns>Converter instance</returns>
    public abstract CsvConverter<T> Create(Type type, CsvOptions<T> options);
}
