using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

/// <inheritdoc cref="CsvBooleanTextValuesAttribute"/>
public sealed class CsvBooleanUtf8ValuesAttribute : CsvConverterAttribute<byte>
{
    /// <summary>
    /// Values that represent <see langword="true"/>.
    /// </summary>
    public string[] TrueValues { get; set; } = [];

    /// <summary>
    /// Values that represent <see langword="false"/>.
    /// </summary>
    public string[] FalseValues { get; set; } = [];

    /// <inheritdoc/>
    protected override CsvConverter<byte> CreateConverterOrFactory(Type targetType, CsvOptions<byte> options)
    {
        CustomBooleanUtf8Converter converter = new(
            options: options,
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
