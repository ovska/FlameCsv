using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser factory for non-flags enums.
/// </summary>
public sealed class EnumTextParserFactory : ICsvParserFactory<char>
{
    /// <inheritdoc cref="EnumTextParser{TEnum}.AllowUndefinedValues"/>
    public bool AllowUndefinedValues { get; }

    /// <inheritdoc cref="EnumTextParser{TEnum}.IgnoreCase"/>
    public bool IgnoreCase { get; }

    public EnumTextParserFactory(
        bool allowUndefinedValues = false,
        bool ignoreCase = true)
    {
        AllowUndefinedValues = allowUndefinedValues;
        IgnoreCase = ignoreCase;
    }

    public bool CanParse(Type resultType)
    {
        return resultType.IsEnum && resultType.GetCustomAttribute<FlagsAttribute>(inherit: false) is null;
    }

    public ICsvParser<char> Create(Type resultType, CsvReaderOptions<char> options)
    {
        bool allowUndefinedValues;
        bool ignoreCase;

        if (options is CsvTextReaderOptions o)
        {
            allowUndefinedValues = o.AllowUndefinedEnumValues;
            ignoreCase = o.IgnoreEnumCase;
        }
        else
        {
            allowUndefinedValues = AllowUndefinedValues;
            ignoreCase = IgnoreCase;
        }

        return GetParserType(resultType).CreateInstance<ICsvParser<char>>(allowUndefinedValues, ignoreCase);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumTextParser<>).MakeGenericType(resultType);
}
