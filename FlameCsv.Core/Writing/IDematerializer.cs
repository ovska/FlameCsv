namespace FlameCsv.Writing;

public interface IDematerializer<T, in TValue> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Amount of fields that will be written when writing a record or header.
    /// </summary>
    public int FieldCount { get; }

    /// <summary>
    /// Formats <typeparamref name="TValue"/> into a CSV record, including the trailing newline.
    /// </summary>
    void Write(ref readonly CsvFieldWriter<T> writer, TValue value);

    /// <summary>
    /// Writes a header, including the trailing newline.
    /// </summary>
    void WriteHeader(ref readonly CsvFieldWriter<T> writer);
}
