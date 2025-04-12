using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the member to use pooled strings.
/// </summary>
/// <seealso cref="CsvOptions{T}.StringPool"/>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvStringPoolingAttribute : CsvConverterAttribute
{
    /// <inheritdoc />
    protected override bool TryCreateConverterOrFactory<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        if (targetType != typeof(string))
        {
            throw new CsvConfigurationException(
                $"{nameof(CsvStringPoolingAttribute)} can only be used to convert strings.");
        }

        StringPool? configured = options.StringPool;

        object? result = null;

        if (typeof(T) == typeof(char))
        {
            result = configured is not null && configured != StringPool.Shared
                ? new PoolingStringTextConverter(configured)
                : PoolingStringTextConverter.SharedInstance;
        }

        if (typeof(T) == typeof(byte))
        {
            result = configured is not null && configured != StringPool.Shared
                ? new PoolingStringUtf8Converter(configured)
                : PoolingStringUtf8Converter.SharedInstance;
        }

        if (result is not null)
        {
            converter = (CsvConverter<T>)result;
            return true;
        }

        converter = null;
        return false;
    }
}
