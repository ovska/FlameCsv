﻿using System.Diagnostics.CodeAnalysis;
using FlameCsv.Formatters;

namespace FlameCsv.Writing;

public interface ICsvFieldWriter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Writes a field to the CSV record.
    /// </summary>
    /// <typeparam name="TValue">Value written</typeparam>
    /// <param name="formatter">Formatter to write the value with</param>
    /// <param name="value">Value to write</param>
    void WriteField<TValue>(ICsvFormatter<T, TValue> formatter, [AllowNull] TValue value);

    /// <summary>
    /// Writes a delimiter to the CSV record.
    /// </summary>
    void WriteDelimiter();

    /// <summary>
    /// Writes a new line to the CSV record.
    /// </summary>
    void WriteNewline();

    /// <summary>
    /// Flushes the underlying writer if the internal buffer is above the flush threshold.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    ValueTask TryFlushAsync(CancellationToken cancellationToken = default);
}