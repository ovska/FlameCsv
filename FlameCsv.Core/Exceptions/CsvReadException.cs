using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Exceptions;

public class CsvReadException : Exception
{
    public CsvReadException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForPrematureEOF<T>(int fieldCount, CsvOptions<T> options, ReadOnlySpan<T> record)
        where T : unmanaged, IEquatable<T>
            => throw new CsvReadException($"Csv record ended prematurely (expected {fieldCount} fields): {options.AsPrintableString(record)}");
}
