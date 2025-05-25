namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors when writing CSV, such as errors thrown during flushing the buffers.
/// </summary>
public sealed class CsvWriteException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// 1-based number of the record that caused the exception, if applicable.
    /// </summary>
    public int? LineNumber { get; init; }
}
