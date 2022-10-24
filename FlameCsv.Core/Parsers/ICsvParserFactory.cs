namespace FlameCsv.Parsers;

/// <summary>
/// Factory for parsers that cannot be created as-is, such as those with generic types.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvParserFactory<T> : ICsvParser<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Creates an instance capable of parsing values of type <paramref name="resultType"/>.
    /// </summary>
    /// <remarks>
    /// This method should only be called after <see cref="ICsvParser{T}.CanParse"/> has verified the type is valid.
    /// </remarks>
    /// <param name="resultType">Value type of the returned <see cref="ICsvParser{T,TValue}"/></param>
    /// <param name="configuration">Current configuration object</param>
    /// <returns>Parser instance</returns>
    ICsvParser<T> Create(Type resultType, CsvConfiguration<T> configuration);
}
