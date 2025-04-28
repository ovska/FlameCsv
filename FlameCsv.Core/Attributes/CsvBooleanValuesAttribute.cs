using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Overrides the converter for <c>bool</c> and <c>bool?</c>.
/// For nullable booleans, attempts to fetch user defined null token from the options via
/// <see cref="CsvOptions{T}.NullTokens"/>.
/// </summary>
public sealed class CsvBooleanValuesAttribute : CsvConverterAttribute
{
    /// <summary>
    /// Values that represent <c>true</c>.
    /// </summary>
    public string[] TrueValues { get; set; } = [];

    /// <summary>
    /// Values that represent <c>false</c>.
    /// </summary>
    public string[] FalseValues { get; set; } = [];

    /// <summary>
    /// If true (the default), uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// Otherwise, uses <see cref="StringComparison.Ordinal"/>.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;

    /// <inheritdoc/>
    protected override bool TryCreateConverterOrFactory<T>(Type targetType, CsvOptions<T> options, [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        CsvConverter<T, bool>? boolConverter = null;

        if (typeof(T) == typeof(char))
        {
            boolConverter = (CsvConverter<T, bool>)(object)new CustomBooleanTextConverter(
                trueValues: TrueValues,
                falseValues: FalseValues,
                ignoreCase: IgnoreCase);
        }

        if (typeof(T) == typeof(byte))
        {
            boolConverter = (CsvConverter<T, bool>)(object)new CustomBooleanUtf8Converter(
                trueValues: TrueValues,
                falseValues: FalseValues,
                ignoreCase: IgnoreCase);
        }

        if (boolConverter is null)
        {
            converter = null;
            return false;
        }

        if (targetType == typeof(bool))
        {
            converter = boolConverter;
            return true;
        }

        if (targetType == typeof(bool?))
        {
            converter = new NullableConverter<T, bool>(boolConverter, options.GetNullToken(typeof(bool?)));
            return true;
        }

        throw new CsvConfigurationException(
            $"{GetType().FullName} was applied on a member with invalid type: {targetType}");
    }
}
