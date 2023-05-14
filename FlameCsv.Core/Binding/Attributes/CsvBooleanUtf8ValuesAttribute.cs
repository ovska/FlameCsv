using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

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
    public override CsvConverter<byte> CreateParser(Type targetType, CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!(TrueValues?.Length > 0 && FalseValues?.Length > 0))
        {
            throw new CsvConfigurationException($"Null/empty true/false values defined for {nameof(CsvBooleanTextValuesAttribute)}");
        }

        BooleanUtf8Converter converter = new(
            standardFormat: (options as CsvUtf8Options)?.BooleanFormat ?? default,
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
