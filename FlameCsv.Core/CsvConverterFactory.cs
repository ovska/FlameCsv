namespace FlameCsv;

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
    /// <returns>
    /// Converter instance, must be <see cref="CsvConverter{T,TValue}"/> where <c>TValue</c> is <paramref name="type"/>.
    /// </returns>
    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    public abstract CsvConverter<T> Create(Type type, CsvOptions<T> options);
}
