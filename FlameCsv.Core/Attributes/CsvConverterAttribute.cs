using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Attributes;

/// <summary>
/// Base attribute for overriding converters for the target member or parameter.
/// </summary>
/// <remarks>
/// The resulting converter is cast to <see cref="CsvConverter{T,TValue}"/>.<br/>
/// This attribute is not recognized by the source generator,
/// use <see cref="CsvConverterAttribute{T,TConverter}"/> instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = true)]
public abstract class CsvConverterAttribute<T> : Attribute where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets or creates a converter instance for the binding's member.
    /// </summary>
    /// <param name="targetType">Type to convert</param>
    /// <param name="options">Current configuration instance</param>
    /// <returns>Converter instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <paramref name="targetType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    protected abstract CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options);

    /// <summary>
    /// Creates the configured converter.
    /// </summary>
    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public CsvConverter<T> CreateConverter(Type targetType, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        CsvConverter<T> instanceOrFactory = CreateConverterOrFactory(targetType, options) ??
            throw new InvalidOperationException($"{GetType()}.{nameof(CreateConverterOrFactory)} returned null");

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
                    instanceOrFactory = NullableConverterFactory<T>.CreateCore(
                        underlying,
                        converter,
                        options.GetNullToken(underlying));
                    goto Success;
                }
            }

            throw new CsvConfigurationException(
                $"Overridden converter {instanceOrFactory.GetType().FullName} " +
                $"can not parse the member type: {targetType.FullName} (attribute: {this.GetType().FullName})");
        }

    Success:
        return instanceOrFactory.GetOrCreateConverter(targetType, options);
    }
}
