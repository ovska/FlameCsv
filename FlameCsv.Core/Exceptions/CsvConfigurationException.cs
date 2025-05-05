namespace FlameCsv.Exceptions;

/// <summary>
/// Represents problems in user code, such as invalid converter types.
/// </summary>
/// <remarks>
/// Initializes an exception representing an erroneus configuration.
/// </remarks>
public class CsvConfigurationException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException);
