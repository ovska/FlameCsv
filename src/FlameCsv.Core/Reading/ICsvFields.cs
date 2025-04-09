namespace FlameCsv.Reading;

/// <summary>
/// Interface representing the fields of a CSV record.
/// </summary>
public interface ICsvFields<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets the number of fields in the record.
    /// </summary>
    int FieldCount { get; }

    /// <summary>
    /// Returns the field at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <remarks>
    /// The span is only valid until another field or the next record is read.
    /// </remarks>
    ReadOnlySpan<T> this[int index] { get; }
}
