namespace FlameCsv.Exceptions;

public sealed class CsvUnhandledException : Exception
{
    /// <summary>
    /// 1-based line index of the erroneus record.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 0-based token index at the start of the record.
    /// </summary>
    public long Position { get; }

    public CsvUnhandledException(
        string message,
        int line,
        long position,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Line = line;
        Position = position;
    }
}
