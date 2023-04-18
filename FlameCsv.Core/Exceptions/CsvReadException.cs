namespace FlameCsv.Exceptions;

public class CsvReadException : Exception
{
    public CsvReadException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
