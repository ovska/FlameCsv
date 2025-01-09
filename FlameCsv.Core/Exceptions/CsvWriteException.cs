namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors when writing CSV, such as errors thrown during flushing the buffers.
/// </summary>
public class CsvWriteException(string? message = null, Exception? innerException = null) : Exception(
    message,
    innerException);
