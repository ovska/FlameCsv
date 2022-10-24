using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;
using FlameCsv.Runtime;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member. A parser or factory of the exact type is used if
/// present in the configuration. Otherwise a new instance of the parameter parser or factory is created.
/// </summary>
/// <remarks>
/// Parsers created this way are not cached in the configuration object, and a new instance is created
/// for every overridden property.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CsvParserOverrideAttribute : Attribute, ICsvParserOverride
{
    /// <summary>
    /// Parser or factory to use for this member.
    /// </summary>
    public Type ParserType { get; }

    /// <inheritdoc cref="CsvParserOverrideAttribute"/>
    /// <param name="parserType">Parser or factory to use</param>
    public CsvParserOverrideAttribute(Type parserType)
    {
        ArgumentNullException.ThrowIfNull(parserType);
        ParserType = parserType;
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="binding">Target member</param>
    /// <param name="configuration">Current configuration instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>Parser instnace</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ParserType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    public ICsvParser<T> CreateParser<T>(CsvBinding binding, CsvConfiguration<T> configuration)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Guard.IsFalse(binding.IsIgnored);

        if (ParserType is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ParserType)} must be set if default {nameof(CreateParser)} implementation is used");
        }

        var targetType = binding.Type;

        foreach (var existing in configuration._parsers)
        {
            if (existing.GetType() == ParserType)
            {
                if (!existing.CanParse(targetType))
                {
                    throw new CsvConfigurationException(
                        $"Existing instance of parser override for {GetMember()} {ParserType.ToTypeString()} "
                        + $"could not parse target member type {targetType.ToTypeString()}");
                }

                return existing.GetParserOrFromFactory(targetType, configuration);
            }
        }

        ICsvParser<T> parserOrFactory;

        try
        {
            parserOrFactory = ActivatorEx.CreateInstance<ICsvParser<T>>(ParserType);
        }
        catch (Exception e)
        {
            string ctorErr = ParserType.GetConstructor(Type.EmptyTypes) is null
                ? " The type does not have a parameterless constructor."
                : "";
            throw new CsvConfigurationException(
                $"Parser override for {GetMember()} {ParserType.ToTypeString()} was not present in the "
                + "configuration, and couldn't be created dynamically."
                + ctorErr,
                innerException: e);
        }

        if (!parserOrFactory.CanParse(targetType))
        {
            throw new CsvConfigurationException(
                $"Dynamically created instance of parser override for {GetMember()} {ParserType.ToTypeString()} "
                + $"can not parse the member type: {targetType.ToTypeString()}");
        }

        return parserOrFactory.GetParserOrFromFactory(targetType, configuration);

        string GetMember() => $"{binding.Member.DeclaringType?.Name}.{binding.Member.Name}".TrimStart('.');
    }
}
