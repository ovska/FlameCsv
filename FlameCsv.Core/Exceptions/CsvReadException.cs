using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Exceptions;

/// <summary>
/// Thrown for faulty but structurally valid CSV, such as wrong field count on a line.
/// </summary>
/// <param name="message"></param>
/// <param name="innerException"></param>
public class CsvReadException(
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Throws an exception for a CSV record ending too early.
    /// </summary>
    /// <param name="fieldCount">Expected number of fields</param>
    /// <param name="options">Current options instance</param>
    /// <param name="record">The current CSV record</param>
    /// <exception cref="CsvReadException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForPrematureEOF<T>(int fieldCount, CsvOptions<T> options, ReadOnlySpan<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new CsvReadException(
            $"Csv record ended prematurely (expected {fieldCount} fields): {options.AsPrintableString(record)}");
    }
}
