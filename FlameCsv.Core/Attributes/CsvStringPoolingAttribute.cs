using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the member to use pooled strings.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvStringPoolingAttribute : CsvConverterAttribute
{
    /// <summary>
    /// Type name of the provider to use for string pooling. The class should have a public static property or a
    /// parameterless method named <see cref="MemberName"/> that returns a <see cref="StringPool"/> instance.
    /// </summary>
    [DAM(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicMethods)]
    public Type? ProviderType { get; init; }

    /// <summary>
    /// Property or method name of the provider to use for string pooling.
    /// </summary>
    public string? MemberName { get; init; }

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

        StringPool? configured = null;

        if (ProviderType is not null && MemberName is not null)
        {
            if (ProviderType is null || MemberName is null)
            {
                throw new CsvConfigurationException(
                    $"Both {nameof(ProviderType)} and {nameof(MemberName)} must be set to use a custom provider.");
            }
            
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            
            MethodInfo provider =
                ProviderType.GetProperty(MemberName, flags)?.GetMethod ??
                ProviderType.GetMethod(MemberName, flags) ??
                throw new CsvConfigurationException($"The provider type {ProviderType} does not have a property or method named {MemberName}.");

            if (provider.GetParameters().Length != 0)
            {
                throw new CsvConfigurationException(
                    $"The provider {ProviderType}.{MemberName}() must be a parameterless method.");
            }

            if (provider.ReturnType != typeof(StringPool))
            {
                throw new CsvConfigurationException(
                    $"The provider {ProviderType}.{MemberName} must return a {nameof(StringPool)}.");
            }

            configured = (StringPool?)provider.Invoke(null, null);
        }

        object? result = null;

        if (typeof(T) == typeof(char))
        {
            result = configured is null || configured == StringPool.Shared
                ? PoolingStringTextConverter.SharedInstance
                : new PoolingStringTextConverter(configured);
        }

        if (typeof(T) == typeof(byte))
        {
            result = configured is null || configured == StringPool.Shared
                ? PoolingStringUtf8Converter.SharedInstance
                : new PoolingStringUtf8Converter(configured);
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
