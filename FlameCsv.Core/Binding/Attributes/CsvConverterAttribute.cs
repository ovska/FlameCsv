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
    /// <inheritdoc cref="CsvHeaderConfigurationAttribute.Scope"/>
    public CsvBindingScope Scope { get; set; }

    private readonly object _cacheKey = new();

    public CsvConverter<T> CreateConverter(Type targetType, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        if (options._explicitCache.TryGetValue(_cacheKey, out CsvConverter<T>? cached))
        {
            return cached;
        }

        CsvConverter<T> instanceOrFactory = CreateConverterOrFactory(targetType, options)
            ?? throw new InvalidOperationException($"{GetType()}.{nameof(CreateConverterOrFactory)} returned null");

        if (!instanceOrFactory.CanConvert(targetType))
        {
            Type? underlying = Nullable.GetUnderlyingType(targetType);

            if (underlying is not null)
            {
                CsvConverter<T>? converter = null;

                if (instanceOrFactory is CsvConverterFactory<T> factory)
                {
                    if (factory.CanConvert(underlying))
                        converter = factory.Create(underlying, options);
                }
                else if (instanceOrFactory.CanConvert(underlying))
                {
                    converter = instanceOrFactory;
                }

                if (converter is not null)
                {
                    instanceOrFactory = NullableConverterFactory<T>.GetParserType(underlying).CreateInstance<CsvConverter<T>>(
                        converter,
                        options.GetNullToken(underlying));
                    goto Success;
                }
            }

            throw new CsvConfigurationException(
                $"Overridden converter {instanceOrFactory.GetType().ToTypeString()} " +
                $"can not parse the member type: {targetType.ToTypeString()}");
        }

        Success:
        var result = instanceOrFactory.GetOrCreateConverter(targetType, options);

        if (options._explicitCache.TryAdd(_cacheKey, result))
            return result;

        return options._explicitCache[_cacheKey];
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="targetType">Type to convert</param>
    /// <param name="options">Current configuration instance</param>
    /// <returns>Converter instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ConverterType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    protected abstract CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options);
}
