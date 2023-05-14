using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member.<para/>A parser or factory of the exact type is used if
/// present in the configuration. Otherwise a new instance of the parameter parser or factory is created. For
/// example, <c>[CsvParserOverride(typeof(MyParser))]</c> will first attempt to use a parser from the configuration
/// with the exact type <c>MyParser</c> before creating it using reflection.
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
    public virtual Type? ParserType { get; }

    /// <inheritdoc cref="CsvConverterAttribute"/>
    /// <param name="parserType">Parser or factory to use</param>
    public CsvConverterAttribute(
        [DynamicallyAccessedMembers(Messages.Ctors)] Type parserType)
    {
        ArgumentNullException.ThrowIfNull(parserType);

        if (!typeof(CsvConverter<T>).IsAssignableFrom(parserType))
        {
            ThrowHelper.ThrowArgumentException("Parser type must be assignable to CsvConverter<T>!");
        }

        ParserType = parserType;
    }

    /// <inheritdoc cref="CsvConverterAttribute"/>
    public CsvConverterAttribute()
    {
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="targetType"></param>
    /// <param name="options">Current configuration instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>Parser instance</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ParserType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    public virtual CsvConverter<T> CreateParser(Type targetType, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(targetType);

        if (ParserType is null)
        {
            ThrowHelper.ThrowInvalidOperationException(
                "Default implementation of CreateParser requires ParserType (was null)");
        }

        CsvConverter<T> parserOrFactory;

        if (ParserType.GetConstructor(new[] { options.GetType() }) is { } exactCtor)
        {
            parserOrFactory = (CsvConverter<T>)exactCtor.Invoke(new object[] { options });
        }
        else if (ParserType.GetConstructor(new[] { typeof(CsvOptions<T>) }) is { } baseTypeCtor)
        {
            parserOrFactory = (CsvConverter<T>)baseTypeCtor.Invoke(new object[] { options });
        }
        else if (ParserType.GetConstructor(Type.EmptyTypes) is { } emptyCtor)
        {
            parserOrFactory = (CsvConverter<T>)emptyCtor.Invoke(Array.Empty<object>());
        }
        else
        {
            throw new CsvConfigurationException(
                $"Parser type {ParserType.ToTypeString()} has no valid constructor!");
        }

        if (!parserOrFactory.CanConvert(targetType))
        {
            throw new CsvConfigurationException(
                $"Dynamically created instance of parser override for {ParserType.ToTypeString()} " +
                $"can not parse the member type: {targetType.ToTypeString()}");
        }

        return parserOrFactory.GetParserOrFromFactory(targetType, options);
    }
}
