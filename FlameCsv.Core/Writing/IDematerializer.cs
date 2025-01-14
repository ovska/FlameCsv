﻿namespace FlameCsv.Writing;

/// <summary>
/// Instance of a type that writes objects/structs as CSV records.
/// </summary>
public interface IDematerializer<T, in TValue> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Number of fields that will be written when writing a record or header.
    /// </summary>
    public int FieldCount { get; }

    /// <summary>
    /// Writes <typeparamref name="TValue"/> as CSV, including the trailing newline.
    /// </summary>
    void Write(ref readonly CsvFieldWriter<T> writer, TValue value);

    /// <summary>
    /// Writes a header, including the trailing newline.
    /// </summary>
    void WriteHeader(ref readonly CsvFieldWriter<T> writer);
}
