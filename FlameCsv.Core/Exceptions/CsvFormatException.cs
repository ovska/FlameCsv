using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Enumeration;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents unrecoverable format errors in the CSV, such as uneven string delimiters.<br/>
/// This exception is <b>not</b> handled by <see cref="CsvValueEnumeratorBase{T,TValue}.ExceptionHandler"/>.
/// </summary>
/// <remarks>
/// Initializes an exception representing invalid CSV format.
/// </remarks>
public sealed class CsvFormatException(
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Throw(string? message = null, Exception? innerException = null)
    {
        throw new CsvFormatException(message, innerException);
    }
}
