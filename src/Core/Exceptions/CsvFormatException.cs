using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Enumeration;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents unrecoverable format errors in the CSV, such as invalid quotes within a field.<br/>
/// This exception is <b>not</b> handled by <see cref="CsvValueEnumeratorBase{T,TValue}.ExceptionHandler"/>.
/// </summary>
/// <remarks>
/// Initializes an exception representing invalid CSV format.
/// </remarks>
public sealed class CsvFormatException(string? message = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Throw(string? message = null, Exception? innerException = null)
    {
        throw new CsvFormatException(message, innerException);
    }

    /// <summary>
    /// 1-based line index of where the invalid data was found, if available.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Approximate 0-based index where the invalid data was found, if available.
    /// </summary>
    public long? Position { get; set; }

    /// <summary>
    /// The CSV record that caused the exception, if available.
    /// </summary>
    public string? Record { get; set; }
}
