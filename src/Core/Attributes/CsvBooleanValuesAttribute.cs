using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Overrides the converter for <c>bool</c> and <c>bool?</c>.
/// For nullable booleans, attempts to fetch user defined <c>null</c> token from the options via
/// <see cref="CsvOptions{T}.NullTokens"/>.
/// </summary>
public sealed class CsvBooleanValuesAttribute : CsvConverterAttribute
{
    /// <summary>
    /// Values that represent <c>true</c>. Must not be empty.
    /// </summary>
    public string[] TrueValues { get; set; } = [];

    /// <summary>
    /// Values that represent <c>false</c>. Must not be empty.
    /// </summary>
    public string[] FalseValues { get; set; } = [];

    /// <summary>
    /// Whether to ignore case when parsing. The default is <c>true</c>.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;

    /// <inheritdoc/>
    protected override bool TryCreateConverterOrFactory<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter
    )
    {
        converter = new CustomBooleanConverter<T>(TrueValues, FalseValues, IgnoreCase);

        if (targetType == typeof(bool))
        {
            return true;
        }

        if (targetType == typeof(bool?))
        {
            converter = new NullableConverter<T, bool>(
                (CsvConverter<T, bool>)converter,
                options.GetNullObject(typeof(bool?))
            );
            return true;
        }

        throw new CsvConfigurationException(
            $"{GetType().FullName} was applied on a member with invalid type: {targetType}"
        );
    }
}
