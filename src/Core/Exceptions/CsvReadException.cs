using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv.Exceptions;

/// <summary>
/// Thrown for faulty but structurally valid CSV.
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
