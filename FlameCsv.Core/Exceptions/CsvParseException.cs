namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of an unparseable value.
/// </summary>
public sealed class CsvParseException : Exception
{
    /// <summary>
    /// Parser instance.
    /// </summary>
    public object? Parser { get; set; }

    /// <summary>
    /// Initializes an exception representing an unparseable value.
    /// </summary>
    public CsvParseException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
