using System.Buffers;
using JetBrains.Annotations;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Provides a base class for formatting enum values.
/// Used by the <see cref="Attributes.CsvEnumConverterAttribute{T,TEnum}"/> source generator.
/// </summary>
[PublicAPI]
public abstract class EnumFormatStrategy<T, TEnum> where T : unmanaged, IBinaryInteger<T> where TEnum : struct, Enum
{
    /// <summary>
    /// Returns a singleton strategy that always fails to format an enum value.
    /// </summary>
    public static EnumFormatStrategy<T, TEnum> None { get; } = new NoneImpl();

    /// <inheritdoc cref="CsvConverter{T,TValue}.TryFormat"/>
    /// <returns>
    /// Status of the formatting operation. <see cref="OperationStatus.Done"/> if the formatting was successful;
    /// <see cref="OperationStatus.InvalidData"/> if the value cannot be formatted by the strategy;
    /// <see cref="OperationStatus.DestinationTooSmall"/> if the destination buffer is too small, and
    /// formatting should be retried with a larger buffer.
    /// </returns>
    public abstract OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten);

    private sealed class NoneImpl : EnumFormatStrategy<T, TEnum>
    {
        public override OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten)
        {
            charsWritten = 0;
            return OperationStatus.InvalidData;
        }
    }
}
