namespace FlameCsv.Exceptions;

/// <summary>
/// Represents problems in user code, such as invalid converter types.
/// </summary>
public class CsvConfigurationException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException);
