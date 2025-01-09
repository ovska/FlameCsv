namespace FlameCsv.Exceptions;

/// <summary>
/// Represents unrecoverable format errors in the CSV, such as uneven string delimiters.
/// This exception is not handled by <see cref="CsvOptions{T}.ExceptionHandler"/>.
/// </summary>
/// <remarks>
/// Initializes an exception representing invalid CSV format.
/// </remarks>
public sealed class CsvFormatException(
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException);
