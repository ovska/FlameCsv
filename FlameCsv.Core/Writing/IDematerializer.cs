using System.Buffers;

namespace FlameCsv.Writing;

public interface IDematerializer<T, in TValue> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Amount of fields that will be written when writing a record or header.
    /// </summary>
    public int FieldCount { get; }

    /// <summary>
    /// Formats <typeparamref name="TValue"/> into a CSV record, including the trailing newline.
    /// </summary>
    void Write<TWriter>(CsvFieldWriter<T, TWriter> writer, TValue value) where TWriter : struct, IBufferWriter<T>;

    /// <summary>
    /// Writes a header if needed, including the trailing newline.
    /// </summary>
    void WriteHeader<TWriter>(CsvFieldWriter<T, TWriter> writer) where TWriter : struct, IBufferWriter<T>;
}
