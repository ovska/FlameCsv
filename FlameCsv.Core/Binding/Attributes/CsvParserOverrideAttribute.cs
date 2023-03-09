using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;
using FlameCsv.Runtime;

namespace FlameCsv.Binding.Attributes;

/// <inheritdoc/>
public sealed class CsvParserOverrideAttribute<T, TParser> : CsvParserOverrideAttribute
    where T : unmanaged, IEquatable<T>
    where TParser : ICsvParser<T>
{
    /// <inheritdoc/>
    public CsvParserOverrideAttribute() : base(typeof(TParser))
    {
    }
}

/// <summary>
/// Overrides the default parser for the target member.<para/>A parser or factory of the exact type is used if
/// present in the configuration. Otherwise a new instance of the parameter parser or factory is created. For
/// example, <c>[CsvParserOverride(typeof(MyParser))]</c> will first attempt to use a parser from the configuration
/// with the exact type <c>MyParser</c> before creating it using reflection.
/// </summary>
/// <remarks>
/// Parsers created this way are not cached in <see cref="CsvReaderOptions{T}"/>,
/// and a new instance is created for every overridden property if necessary.
/// </remarks>
[AttributeUsage(CsvBinding.AllowedOn, AllowMultiple = false)]
public class CsvParserOverrideAttribute : Attribute
{
    /// <summary>
    /// Parser or factory to use for this member.
    /// </summary>
    public virtual Type? ParserType { get; }

    /// <inheritdoc cref="CsvParserOverrideAttribute"/>
    /// <param name="parserType">Parser or factory to use</param>
    public CsvParserOverrideAttribute(Type parserType)
    {
        ArgumentNullException.ThrowIfNull(parserType);
        ParserType = parserType;
    }

    /// <inheritdoc cref="CsvParserOverrideAttribute"/>
    public CsvParserOverrideAttribute()
    {
    }

    /// <summary>
    /// Gets or creates a parser instance for the binding's member.
    /// </summary>
    /// <param name="binding">Target member</param>
    /// <param name="options">Current configuration instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>Parser instnace</returns>
    /// <exception cref="CsvConfigurationException">Thrown if <see cref="ParserType"/> is not valid for the member,
    /// or is not present in the configuration and has no parameterless constructor.</exception>
    public virtual ICsvParser<T> CreateParser<T>(CsvBinding binding, CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        Guard.IsTrue(binding.IsMember);

        if (ParserType is null)
        {
            ThrowHelper.ThrowInvalidOperationException(
                "Default implementation of CreateParser requires ParserType (was null)");
        }

        var targetType = binding.Type;

        foreach (var existing in options.EnumerateParsers())
        {
            if (existing.GetType() == ParserType)
            {
                if (!existing.CanParse(targetType))
                {
                    throw new CsvConfigurationException(
                        $"Existing instance of parser override for {binding} {ParserType.ToTypeString()} "
                        + $"could not parse target member type {targetType.ToTypeString()}");
                }

                return existing.GetParserOrFromFactory(targetType, options);
            }
        }

        ICsvParser<T> parserOrFactory;

        try
        {
            parserOrFactory = ActivatorEx.CreateInstance<ICsvParser<T>>(ParserType);
        }
        catch (Exception e)
        {
            string ctorErr = !ParserType.IsValueType && ParserType.GetConstructor(Type.EmptyTypes) is null
                ? " The type does not have a parameterless constructor."
                : "";
            throw new CsvConfigurationException(
                $"Parser override for {binding} {ParserType.ToTypeString()} was not present in the "
                + "configuration, and couldn't be created dynamically."
                + ctorErr,
                innerException: e);
        }

        if (!parserOrFactory.CanParse(targetType))
        {
            throw new CsvConfigurationException(
                $"Dynamically created instance of parser override for {binding} "
                + $"{ParserType.ToTypeString()} can not parse the member type: {targetType.ToTypeString()}");
        }

        return parserOrFactory.GetParserOrFromFactory(targetType, options);
    }
}
