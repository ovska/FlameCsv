using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Base attribute for overriding converters for the target member or parameter.
/// </summary>
/// <remarks>
/// The resulting converter is cast to <see cref="CsvConverter{T,TValue}"/>.<br/>
/// This attribute is not recognized by the source generator,
/// use <see cref="CsvConverterAttribute{TConverter}"/> instead.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter,
    // allowmultiple needed so both char and byte converters can be applied to the same member
    AllowMultiple = true
)]
public abstract class CsvConverterAttribute : Attribute
{
    /// <summary>
    /// Attempts to create a converter for the target member or parameter.<br/>
    /// If the configured converter is not for token type <typeparamref name="T"/>, this method should return <c>false</c>.<br/>
    /// If the converter is not for the target type, this method should throw a <see cref="CsvConfigurationException"/>.
    /// </summary>
    /// <param name="targetType">Type to convert</param>
    /// <param name="options">Current configuration instance</param>
    /// <param name="converter"></param>
    /// <returns>Converter instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <paramref name="targetType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    protected abstract bool TryCreateConverterOrFactory<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter
    )
        where T : unmanaged, IBinaryInteger<T>;

    /// <summary>
    /// Creates the configured converter.
    /// </summary>
    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public bool TryCreateConverter<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        if (!TryCreateConverterOrFactory(targetType, options, out converter))
        {
            return false;
        }

        if (!converter.CanConvert(targetType))
        {
            Type? underlying = Nullable.GetUnderlyingType(targetType);

            if (underlying is not null)
            {
                if (converter.CanConvert(underlying))
                {
                    converter = NullableConverterFactory<T>.CreateCore(underlying, converter, options);
                    goto Success;
                }

                if (converter is CsvConverterFactory<T> factory && factory.CanConvert(underlying))
                {
                    converter = factory.Create(underlying, options);
                    goto Success;
                }
            }

            throw new CsvConfigurationException(
                $"Overridden converter {converter.GetType().FullName} "
                    + $"can not parse the member type: {targetType.FullName} (attribute: {this.GetType().FullName})"
            );
        }

        Success:
        converter = converter.GetAsConverter(targetType, options);
        return true;
    }
}
