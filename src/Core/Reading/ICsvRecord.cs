namespace FlameCsv.Reading;

/// <summary>
/// Interface representing the fields of a CSV record.
/// </summary>
public interface ICsvRecord<T>
    where T : unmanaged, IBinaryInteger<T>
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
    /// The span is only guaranteed to be valid until another field or the next record is read.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    ReadOnlySpan<T> this[int index] { get; }

    /// <summary>
    /// Returns the raw value of the record, not including trailing newline.
    /// </summary>
    /// <remarks>
    /// The span is only guaranteed to be valid until the next record is read.
    /// </remarks>
    ReadOnlySpan<T> Raw { get; }
}
