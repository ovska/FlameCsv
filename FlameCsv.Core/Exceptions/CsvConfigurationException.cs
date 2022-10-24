namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors in configuration code, such as invalid parser types or factories.
/// </summary>
public class CsvConfigurationException : Exception
{
    public CsvConfigurationException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
