namespace FlameCsv.Reading;

/// <summary>
/// Instance of a type that reads CSV records into objects/structs.
/// </summary>
public interface IMaterializer<T, out TResult> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Parses <typeparamref name="TResult"/> from the CSV record.
    /// </summary>
    /// <param name="reader">Field reader</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="Exceptions.CsvFormatException">
    /// Thrown if the data is invalid (e.g., wrong number of fields)
    /// </exception>
    /// <exception cref="Exceptions.CsvParseException">Thrown if a value cannot be parsed</exception>
    TResult Parse<TRecord>(scoped ref TRecord reader)
       where TRecord : ICsvRecord<T>, allows ref struct;
}
