using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Exceptions;

/// <summary>
/// Thrown for faulty but structurally valid CSV, such as wrong field count on a line.
/// </summary>
/// <param name="message"></param>
/// <param name="innerException"></param>
public sealed class CsvReadException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Throws an exception for a CSV record having an invalid number of fields.
    /// </summary>
    /// <exception cref="CsvReadException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForInvalidFieldCount(int expected, int actual)
    {
        throw new CsvReadException($"Expected {expected} fields, but the record had {actual}");
    }
}
