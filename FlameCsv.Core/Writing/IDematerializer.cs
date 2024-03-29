﻿using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Writing;
    
public interface IDematerializer<T, in TValue> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Formats <typeparamref name="TValue"/> into a CSV record, including the trailing newline.
    /// </summary>
    void Write<TWriter>(CsvFieldWriter<T, TWriter> writer, [AllowNull] TValue value)
        where TWriter : struct, IBufferWriter<T>;

    /// <summary>
    /// Writes a header if needed, including the trailing newline.
    /// </summary>
    void WriteHeader<TWriter>(CsvFieldWriter<T, TWriter> writer)
        where TWriter : struct, IBufferWriter<T>;
}
