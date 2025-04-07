using System.Buffers;
using JetBrains.Annotations;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Provides a base class for parsing enum values.
/// Used by the <see cref="Attributes.CsvEnumConverterAttribute{T,TEnum}"/> source generator.
/// </summary>
[PublicAPI]
public abstract class EnumParseStrategy<T, TEnum> where T : unmanaged, IBinaryInteger<T> where TEnum : struct, Enum
{
    /// <summary>
    /// Returns a singleton strategy that always fails to parse an enum value.
    /// </summary>
    public static EnumParseStrategy<T, TEnum> None { get; } = new NoneImpl();

    /// <inheritdoc cref="CsvConverter{T,TValue}.TryParse"/>
    /// <returns>
    /// Status of the parsing operation. <see cref="OperationStatus.Done"/> if the parsing was successful;
    /// <see cref="OperationStatus.InvalidData"/> if the value cannot be parsed by the strategy.
    /// </returns>
    public abstract bool TryParse(ReadOnlySpan<T> source, out TEnum value);

    private sealed class NoneImpl : EnumParseStrategy<T, TEnum>
    {
        public override bool TryParse(ReadOnlySpan<T> source, out TEnum value)
        {
            value = default;
            return false;
        }
    }
}
