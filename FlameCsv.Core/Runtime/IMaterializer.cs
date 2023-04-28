using FlameCsv.Reading;

namespace FlameCsv.Runtime;

/// <summary>
/// Instance of a type that reads CSV records into objects/structs.
/// </summary>
internal interface IMaterializer<T, out TResult> where T : unmanaged, IEquatable<T>
{
    /// <summary>Amount of fields required to create a value.</summary>
    int FieldCount { get; }

    /// <summary>
    /// Parses <typeparamref name="TResult"/> from the CSV record.
    /// </summary>
    /// <param name="state">State containing the enumerated record</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="Exceptions.CsvFormatException">
    /// Thrown if the data is invalid (e.g. wrong field count)
    /// </exception>
    /// <exception cref="Exceptions.CsvParseException">Thrown if a value cannot be parsed</exception>
    TResult Parse(ref CsvEnumerationStateRef<T> state);

    TResult Parse(ReadOnlySpan<ReadOnlyMemory<T>> fields, in CsvReadingContext<T> context);
}
