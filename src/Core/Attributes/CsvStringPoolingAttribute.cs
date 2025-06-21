using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the member to use pooled strings.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvStringPoolingAttribute : CsvConverterAttribute
{
    /// <inheritdoc />
    protected override bool TryCreateConverterOrFactory<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter
    )
    {
        if (targetType != typeof(string))
        {
            throw new CsvConfigurationException(
                $"{nameof(CsvStringPoolingAttribute)} can only be used to convert strings."
            );
        }

        if (typeof(T) == typeof(char))
        {
            converter = (CsvConverter<T>)(object)CsvPoolingStringTextConverter.Instance;
            return true;
        }

        if (typeof(T) == typeof(byte))
        {
            converter = (CsvConverter<T>)(object)CsvPoolingStringUtf8Converter.Instance;
            return true;
        }

        throw Token<T>.NotSupported;
    }
}
