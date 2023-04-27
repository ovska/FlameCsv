namespace FlameCsv.Exceptions;

/// <summary>
/// Represents unrecoverable format errors in the CSV.
/// Exceptions of this kind cannot be handled by <see cref="CsvReaderOptions{T}.ExceptionHandler"/>.
/// </summary>
public sealed class CsvFormatException : Exception
{
    /// <summary>
    /// Initializes an exception representing invalid CSV format.
    /// </summary>
    public CsvFormatException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
