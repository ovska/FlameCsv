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
    public string[] TrueValues { get; set; } = [];

    /// <summary>
    /// Values that represent <see langword="false"/>.
    /// </summary>
    public string[] FalseValues { get; set; } = [];

    /// <summary>
    /// Comparison type to use, default is null which uses <see cref="CsvOptions{T}.Comparer"/> from options.
    /// </summary>
    /// <remarks>
    /// If used, the options' comparer must implement <see cref="IAlternateEqualityComparer{TAlternate,T}"/> for span.
    /// </remarks>
    public StringComparison? Comparison { get; set; }

    /// <inheritdoc/>
    protected override CsvConverter<char> CreateConverterOrFactory(Type targetType, CsvOptions<char> options)
    {
        CustomBooleanTextConverter converter = new(
            trueValues: TrueValues,
            falseValues: FalseValues,
            comparison: Comparison,
            options: options);

        if (targetType == typeof(bool))
        {
            return converter;
        }

        if (targetType == typeof(bool?))
        {
            return new NullableConverter<char, bool>(converter, options.GetNullToken(typeof(bool)));
        }

        throw new CsvConfigurationException(
            $"{nameof(CsvBooleanTextValuesAttribute)} is valid on bool and bool?, but was used on type {targetType.FullName}");
    }
}
