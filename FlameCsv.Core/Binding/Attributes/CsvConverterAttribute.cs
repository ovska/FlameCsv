using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Base attribute for overriding converters for the target member or parameter.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    AllowMultiple = true,
    Inherited = true)]
public abstract class CsvConverterAttribute<T> : Attribute, ICsvBindingAttribute where T : unmanaged, IEquatable<T>
{
    protected CsvConverterAttribute()
    {
    }

    /// <inheritdoc cref="CsvHeaderAttribute.Scope"/>
    public CsvBindingScope Scope { get; set; }

    public CsvConverter<T> CreateConverter(Type targetType, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        var instanceOrFactory = CreateConverterOrFactory(targetType, options);

        if (!instanceOrFactory.CanConvert(targetType))
        {
            if (instanceOrFactory is CsvConverterFactory<T>)
            {
                throw new CsvConfigurationException(
                    $"Overridden converter factory {instanceOrFactory.GetType().ToTypeString()} " +
                    $"can not parse the member type: {targetType.ToTypeString()}");
            }

            if (CastingConverter<T>.TryCreate(instanceOrFactory, targetType, options) is not { } casting)
            {
                throw new CsvConfigurationException(
                    $"Overridden converter {instanceOrFactory.GetType().ToTypeString()} " +
                    $"can not parse the member type: {targetType.ToTypeString()}");
            }

            Debug.Assert(casting.CanConvert(targetType));
            return casting;
        }

        return instanceOrFactory.GetOrCreateConverter(targetType, options);
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="targetType"></param>
    /// <param name="options">Current configuration instance</param>
    /// <returns>Converter instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ConverterType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    protected abstract CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options);
}
