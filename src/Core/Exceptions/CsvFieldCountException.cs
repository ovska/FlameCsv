using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv.Exceptions;

/// <summary>
/// Thrown when a CSV record has a different number of fields than expected.
/// </summary>
public class CsvFieldCountException(string? message = null, Exception? innerException = null)
    : CsvReadException(message, innerException)
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
    /// 0-based parallel chunk index.
    /// </summary>
    internal int? ChunkOrder { get; set; }

    /// <inheritdoc/>
    public override string Message
    {
        get
        {
            if (GetType() != typeof(CsvFieldCountException) || !RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return base.Message;
            }

            using var vsb = new ValueStringBuilder(stackalloc char[256]);
            vsb.Append(base.Message);
            vsb.Append(" - ");
            vsb.Append("Line: ");
            vsb.AppendFormatted(Line ?? -1);
            vsb.Append(", Position: ");
            vsb.AppendFormatted(RecordPosition ?? -1);

            if (ChunkOrder.HasValue)
            {
                vsb.Append(", Parallel chunk index: ");
                vsb.AppendFormatted(ChunkOrder.Value);
            }

            return vsb.ToString();
        }
    }

    /// <summary>
    /// Throws an exception for a CSV record having an invalid number of fields.
    /// </summary>
    /// <remarks>
    /// This API supports code produced by the FlameCsv source generator and is not intended to be used directly.
    /// Its compatibility is only guaranteed for the way generated code uses it; direct use from user code is not
    /// supported, and its behavior may change in a minor release.
    /// </remarks>
    /// <exception cref="CsvFieldCountException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowForInvalidFieldCount<T>(int expected, CsvRecordRef<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new CsvFieldCountException(
            $"Expected {expected} fields, but the record had {record.FieldCount}: {record.Raw.AsPrintableString()}"
        )
        {
            ExpectedFieldCount = expected,
            ActualFieldCount = record.FieldCount,
        };
    }
}
