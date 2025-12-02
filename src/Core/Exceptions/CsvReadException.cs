using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

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

    internal virtual void Enrich<T>(int line, long position, ref readonly CsvSlice<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        Line ??= line;
        RecordPosition ??= position;
        RecordValue ??= record.RawValue.AsPrintableString();
    }
}

/// <summary>
/// Thrown for faulty but structurally valid CSV, such as wrong field count on a line.
/// </summary>
public class CsvReadException(string? message = null, Exception? innerException = null)
    : CsvReadExceptionBase(message, innerException)
{
    /// <summary>
    /// The expected field count.
    /// </summary>
    public required int ExpectedFieldCount { get; init; }

    /// <summary>
    /// The actual field count.
    /// </summary>
    public required int ActualFieldCount { get; init; }

    /// <summary>
    /// Throws an exception for a CSV record having an invalid number of fields.
    /// </summary>
    /// <exception cref="CsvReadException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForInvalidFieldCount<T, TRecord>(
        int expected,
        int actual,
        scoped ref readonly TRecord record
    )
        where T : unmanaged, IBinaryInteger<T>
        where TRecord : ICsvRecord<T>, allows ref struct
    {
        throw new CsvReadException(
            $"Expected {expected} fields, but the record had {actual}: {Transcode.ToString(record.Raw)}"
        )
        {
            ExpectedFieldCount = expected,
            ActualFieldCount = actual,
        };
    }
}
