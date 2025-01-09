namespace FlameCsv.Exceptions;

/// <summary>
/// Wraps unhandled exceptions thrown when reading CSV records into types.
/// </summary>
public sealed class CsvUnhandledException(
    string? message,
    int line,
    long position,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// 1-based line index of the erroneus record.
    /// </summary>
    public int Line { get; } = line;

    /// <summary>
    /// 0-based token index in the data, counted at the start of the record.
    /// </summary>
    public long Position { get; } = position;
}
