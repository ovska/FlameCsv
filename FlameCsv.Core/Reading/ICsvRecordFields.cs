namespace FlameCsv.Reading;

/// <summary>
/// Interface representing the fields of a CSV record.
/// </summary>
public interface ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets the CSV options associated with the record.
    /// </summary>
    CsvOptions<T> Options { get; }

    /// <summary>
    /// Gets the number of fields in the record.
    /// </summary>
    int FieldCount { get; }

    /// <summary>
    /// Returns the field at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    ReadOnlySpan<T> this[int index] { get; }
}
