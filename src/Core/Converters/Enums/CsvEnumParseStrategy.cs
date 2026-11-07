using JetBrains.Annotations;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Provides a base class for parsing enum values.
/// Used by the <see cref="Attributes.CsvEnumConverterAttribute{T,TEnum}"/> source generator.
/// </summary>
/// <remarks>
/// This API supports code produced by the FlameCsv source generator and is not intended to be used directly.
/// Its compatibility is only guaranteed for the way generated code uses it; direct use from user code is not
/// supported, and its behavior may change in a minor release.
/// </remarks>
[PublicAPI]
public abstract class CsvEnumParseStrategy<T, TEnum>
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum
{
    /// <inheritdoc cref="CsvConverter{T,TValue}.TryParse"/>
    /// <returns>
    /// <see langword="true"/> if the value was parsed successfully;
    /// <see langword="false"/> if the value cannot be parsed by the strategy.
    /// </returns>
    /// <remarks>The source buffer is never empty.</remarks>
    public abstract bool TryParse(ReadOnlySpan<T> source, out TEnum value);
}
