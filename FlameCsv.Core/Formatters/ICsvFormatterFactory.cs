using FlameCsv.Writers;

namespace FlameCsv.Formatters;

/// <summary>
/// Factory for formatters that cannot be created as-is, such as those with generic types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatterFactory<T> : ICsvFormatter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Creates an instance capable of formatting values of the specified type.
    /// </summary>
    /// <remarks>
    /// This method should only be called after <see cref="ICsvFormatter{T}.CanFormat"/> has verified the type is valid.
    /// </remarks>
    /// <param name="valueType">Value type of the <see cref="ICsvFormatter{T,TValue}"/></param>
    /// <param name="options">Current options instance</param>
    /// <returns>Formatter instance</returns>
    ICsvFormatter<T> Create(Type valueType, CsvWriterOptions<T> options);

    /// <inheritdoc cref="Create(Type, CsvWriterOptions{T})"/>
    ICsvFormatter<T, TValue> Create<TValue>(CsvWriterOptions<T> options)
        => (ICsvFormatter<T, TValue>)Create(typeof(TValue), options);
}
