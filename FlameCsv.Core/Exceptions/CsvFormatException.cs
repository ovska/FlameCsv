namespace FlameCsv.Exceptions;

/// <summary>
/// Represents format errors in the read CSV, such as invalid column counts in a row.
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
