namespace FlameCsv.Writing;

/// <summary>
/// Instance of a type that writes objects/structs as CSV records.
/// </summary>
public interface IDematerializer<T, in TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Number of fields the instance will write.
    /// </summary>
    public int FieldCount { get; }

    /// <summary>
    /// Writes <typeparamref name="TValue"/> as CSV as <see cref="FieldCount"/> fields.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline.
    /// </remarks>
    void Write(CsvFieldWriter<T> writer, TValue value);

    /// <summary>
    /// Writes the header for <typeparamref name="TValue"/> as <see cref="FieldCount"/> fields.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline.
    /// </remarks>
    void WriteHeader(CsvFieldWriter<T> writer);
}
