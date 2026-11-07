using System.Buffers;
using JetBrains.Annotations;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Provides a base class for formatting enum values.
/// Used by the <see cref="Attributes.CsvEnumConverterAttribute{T,TEnum}"/> source generator.
/// </summary>
/// <remarks>
/// This API supports code produced by the FlameCsv source generator and is not intended to be used directly.
/// Its compatibility is only guaranteed for the way generated code uses it; direct use from user code is not
/// supported, and its behavior may change in a minor release.
/// </remarks>
[PublicAPI]
public abstract class CsvEnumFormatStrategy<T, TEnum>
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Returns a singleton strategy that always fails to format an enum value.
    /// </summary>
    public static CsvEnumFormatStrategy<T, TEnum> None => field ??= new NoneImpl();

    /// <inheritdoc cref="CsvConverter{T,TValue}.TryFormat"/>
    /// <returns>
    /// Status of the formatting operation. <see cref="OperationStatus.Done"/> if the formatting was successful;
    /// <see cref="OperationStatus.InvalidData"/> if the value cannot be formatted by the strategy;
    /// <see cref="OperationStatus.DestinationTooSmall"/> if the destination buffer is too small, and
    /// formatting should be retried with a larger buffer.
    /// </returns>
    /// <remarks>The destination buffer is never empty.</remarks>
    public abstract OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten);

    private sealed class NoneImpl : CsvEnumFormatStrategy<T, TEnum>
    {
        public override OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten)
        {
            charsWritten = 0;
            return OperationStatus.InvalidData;
        }
    }
}
