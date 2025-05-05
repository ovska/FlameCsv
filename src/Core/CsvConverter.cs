namespace FlameCsv;

/// <summary>
/// Base class used for registering custom converters.<br/>
/// Implement a converter
/// by inheriting either <see cref="CsvConverter{T, TValue}"/> or <see cref="CsvConverterFactory{T}"/>
/// </summary>
/// <remarks>
/// Converter instances must be safe to use concurrently.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public abstract class CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter, or a suitable converter can be
    /// created from this factory instance.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><c>true</c> if the converter is suitable for <paramref name="type"/></returns>
    public abstract bool CanConvert(Type type);

    /// <summary>
    /// Constructor to prevent direct inheritance.
    /// </summary>
    private protected CsvConverter()
    {
    }
}
