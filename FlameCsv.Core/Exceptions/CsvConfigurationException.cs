namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors in configuration code, such as invalid parser types or factories.
/// </summary>
public class CsvConfigurationException : Exception
{
    /// <summary>
    /// Initializes an exception representing an erroneus configuration.
    /// </summary>
    public CsvConfigurationException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
