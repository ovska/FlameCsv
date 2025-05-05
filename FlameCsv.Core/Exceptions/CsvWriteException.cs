namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors when writing CSV, such as errors thrown during flushing the buffers.
/// </summary>
public sealed class CsvWriteException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    internal static CsvWriteException OnComplete(Exception ex) =>
        new("Exception occured while writing leftover data while completing the writer.", ex);
}
