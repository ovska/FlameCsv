using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents format errors in the read CSV, such as invalid column counts in a row.
/// </summary>
public sealed class CsvFormatException : Exception
{
    /// <summary>
    /// Initializes an exception representing invalid CSV format.
    /// </summary>
    public CsvFormatException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    [StackTraceHidden, DoesNotReturn]
    internal static void Throw<T>(
        CsvFormatException? inner,
        ReadOnlySpan<T> line,
        bool exposeContents,
        in CsvDialect<T> tokens)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format - {UtilityExtensions.AsPrintableString(line, exposeContents, in tokens)}",
            inner);
    }

    [StackTraceHidden, DoesNotReturn]
    internal static void Throw<T>(
        string message,
        ReadOnlySpan<T> line,
        bool exposeContents,
        in CsvDialect<T> tokens)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"{message}: {UtilityExtensions.AsPrintableString(line, exposeContents, in tokens)}");
    }
}
