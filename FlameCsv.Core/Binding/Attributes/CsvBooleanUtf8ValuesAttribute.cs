using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

/// <inheritdoc cref="CsvBooleanTextValuesAttribute"/>
public sealed class CsvBooleanUtf8ValuesAttribute : CsvConverterAttribute<byte>
{
    /// <summary>
    /// Values that represent <see langword="true"/>.
    /// </summary>
    public string[] TrueValues { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Values that represent <see langword="false"/>.
    /// </summary>
    public string[] FalseValues { get; set; } = Array.Empty<string>();

    /// <inheritdoc/>
    public override CsvConverter<byte> CreateConverter(Type targetType, CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!(TrueValues?.Length > 0 && FalseValues?.Length > 0))
        {
            throw new CsvConfigurationException($"Null/empty true/false values defined for {nameof(CsvBooleanTextValuesAttribute)}");
        }

        CustomBooleanUtf8Converter converter = new(
            trueValues: TrueValues,
            falseValues: FalseValues);

        if (targetType == typeof(bool))
        {
            return converter;
        }

        if (targetType == typeof(bool?))
        {
            return new NullableConverter<byte, bool>(converter, options.GetNullToken(typeof(bool?)));
        }

        throw new CsvConfigurationException(
            $"{nameof(CsvBooleanTextValuesAttribute)} was applied on a member with invalid type: {targetType}");
    }
}
