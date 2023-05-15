using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default converter for the target member.
/// </summary>
/// <remarks>
/// Parsers created this way are not cached in <see cref="CsvOptions{T}"/>,
/// and a new instance is created for every overridden property if necessary.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class CsvConverterAttribute<T> : Attribute where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Parser or factory to use for this member.
    /// </summary>
    [DynamicallyAccessedMembers(Messages.Ctors)]
    public virtual Type? ConverterType { get; }

    /// <inheritdoc cref="CsvConverterAttribute"/>
    /// <param name="converterType">Converter or factory to use</param>
    public CsvConverterAttribute(
        [DynamicallyAccessedMembers(Messages.Ctors)] Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);

        if (!typeof(CsvConverter<T>).IsAssignableFrom(converterType))
        {
            ThrowHelper.ThrowArgumentException("Converter type must be assignable to CsvConverter<T>!");
        }

        ConverterType = converterType;
    }

    /// <inheritdoc cref="CsvConverterAttribute"/>
    protected CsvConverterAttribute()
    {
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="targetType"></param>
    /// <param name="options">Current configuration instance</param>
    /// <returns>Converter instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ConverterType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    public virtual CsvConverter<T> CreateConverter(Type targetType, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        if (ConverterType is null)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Default implementation of {nameof(CreateConverter)} requires {nameof(ConverterType)} (was null)");
        }

        if (GetType() != typeof(CsvConverterAttribute<T>) &&
            !typeof(CsvConverter<T>).IsAssignableFrom(ConverterType))
        {
            ThrowHelper.ThrowArgumentException("Converter type must be assignable to CsvConverter<T>!");
        }

        CsvConverter<T> instanceOrFactory;

        if (ConverterType.GetConstructor(new[] { options.GetType() }) is { } exactCtor)
        {
            instanceOrFactory = (CsvConverter<T>)exactCtor.Invoke(new object[] { options });
        }
        else if (ConverterType.GetConstructor(new[] { typeof(CsvOptions<T>) }) is { } baseTypeCtor)
        {
            instanceOrFactory = (CsvConverter<T>)baseTypeCtor.Invoke(new object[] { options });
        }
        else if (ConverterType.GetConstructor(Type.EmptyTypes) is { } emptyCtor)
        {
            instanceOrFactory = (CsvConverter<T>)emptyCtor.Invoke(Array.Empty<object>());
        }
        else
        {
            throw new CsvConfigurationException(
                $"Parser type {ConverterType.ToTypeString()} has no valid constructor!");
        }

        if (!instanceOrFactory.CanConvert(targetType))
        {
            throw new CsvConfigurationException(
                $"Dynamically created instance of parser override for {ConverterType.ToTypeString()} " +
                $"can not parse the member type: {targetType.ToTypeString()}");
        }

        return instanceOrFactory.GetParserOrFromFactory(targetType, options);
    }
}
