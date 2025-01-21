using FlameCsv.Exceptions;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for <c>bool</c> and <c>bool?</c>.
/// For nullable booleans, attempts to fetch user defined null token from the options via
/// <see cref="CsvOptions{T}.NullTokens"/>.
/// </summary>
public sealed class CsvBooleanValuesAttribute<T> : CsvConverterAttribute<T> where T : unmanaged, IBinaryInteger<T>
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
    /// If true (the default), uses <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// Otherwise, uses <see cref="StringComparison.Ordinal"/>.
    /// </summary>
    public bool IgnoreCase { get; set; } = true;

    /// <inheritdoc/>
    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        object? converter = null;

        if (typeof(T) == typeof(char))
        {
            converter = new CustomBooleanTextConverter(
                trueValues: TrueValues,
                falseValues: FalseValues,
                comparison: IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal,
                options: (CsvOptions<char>)(object)options);
        }

        if (typeof(T) == typeof(byte))
        {
            converter = new CustomBooleanUtf8Converter(
                options: (CsvOptions<byte>)(object)options,
                trueValues: TrueValues,
                falseValues: FalseValues,
                ignoreCase: IgnoreCase);
        }

        if (converter is null)
        {
            throw new NotSupportedException($"{GetType().FullName} does not support token type {Token<T>.Name}");
        }

        if (targetType == typeof(bool))
        {
            return (CsvConverter<T>)converter;
        }

        if (targetType == typeof(bool?))
        {
            return new NullableConverter<T, bool>(
                (CsvConverter<T, bool>)converter,
                options.GetNullToken(typeof(bool?)));
        }

        throw new CsvConfigurationException(
            $"{GetType().FullName} was applied on a member with invalid type: {targetType}");
    }
}
