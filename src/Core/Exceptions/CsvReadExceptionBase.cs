using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Exceptions;

/// <summary>
/// Base class for exceptions thrown while reading CSV data.
/// </summary>
public abstract class CsvReadExceptionBase(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// 1-based line index of where the invalid data was found, if available.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Approximate 0-based index where the invalid data was found, if available.
    /// </summary>
    public long? RecordPosition { get; set; }

    /// <summary>
    /// Raw value of the record as a <see cref="string"/>, if available.
    /// </summary>
    public string? RecordValue { get; set; }

    internal virtual void Enrich<T>(int line, long position, RecordView view, CsvReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        Line ??= line;
        RecordPosition ??= position;

        try
        {
            RecordValue ??= view.GetRecord(reader).AsPrintableString();
        }
        catch
        {
            // ignored
        }
    }
}
