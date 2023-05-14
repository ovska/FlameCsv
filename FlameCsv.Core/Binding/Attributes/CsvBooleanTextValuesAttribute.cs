using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member. Applicable for <c>bool</c> and <c>bool?</c>
/// when parsing text or UTF8 bytes.<br/>
/// For nullable booleans, attempts to fetch user defined null token from the options via
/// <see cref="ICsvNullTokenConfiguration{T}"/>.
/// </summary>
public sealed class CsvBooleanTextValuesAttribute : CsvConverterAttribute<char> // TODO: make generic by T
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
    public override CsvConverter<char> CreateParser(Type targetType, CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!(TrueValues?.Length > 0 && FalseValues?.Length > 0))
        {
            throw new CsvConfigurationException($"Null/empty true/false values defined for {nameof(CsvBooleanTextValuesAttribute)}");
        }

        BooleanTextConverter converter = new(
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
            $"{nameof(CsvBooleanTextValuesAttribute)} was applied on a member with invalid type: {targetType}");
    }
}
