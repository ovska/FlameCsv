namespace FlameCsv.Exceptions;

public class CsvWriteException : Exception
{
    public CsvWriteException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
