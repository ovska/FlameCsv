using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for <c>bool</c> and <c>bool?</c>.
/// For nullable booleans, attempts to fetch user defined null token from the options via
/// <see cref="CsvOptions{T}.NullTokens"/>.
/// </summary>
public sealed class CsvBooleanTextValuesAttribute : CsvConverterAttribute<char>
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
    protected override CsvConverter<char> CreateConverterOrFactory(Type targetType, CsvOptions<char> options)
    {
        CustomBooleanTextConverter converter = new(
            trueValues: TrueValues,
            falseValues: FalseValues);

        if (targetType == typeof(bool))
        {
            return converter;
        }

        if (targetType == typeof(bool?))
        {
            return new NullableConverter<char, bool>(converter, options.GetNullToken(typeof(bool?)));
        }

        throw new CsvConfigurationException(
            $"{nameof(CsvBooleanTextValuesAttribute)} is valid on bool and bool?, but was on type {targetType.FullName}");
    }
}
