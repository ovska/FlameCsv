using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

public class EnumUtf8ParserFactory : ICsvParserFactory<byte>
{
    /// <inheritdoc cref="EnumUtf8Parser{TEnum}.AllowUndefinedValues"/>
    public bool AllowUndefinedValues { get; }

    /// <inheritdoc cref="EnumUtf8Parser{TEnum}.IgnoreCase"/>
    public bool IgnoreCase { get; }

    public EnumUtf8ParserFactory(
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

    public ICsvParser<byte> Create(Type resultType, CsvReaderOptions<byte> options)
    {
        bool allowUndefinedValues;
        bool ignoreCase;

        if (options is CsvUtf8ReaderOptions o)
        {
            allowUndefinedValues = o.AllowUndefinedEnumValues;
            ignoreCase = o.IgnoreEnumCase;

        }
        else
        {
            allowUndefinedValues = AllowUndefinedValues;
            ignoreCase = IgnoreCase;
        }

        return GetParserType(resultType).CreateInstance<ICsvParser<byte>>(allowUndefinedValues, ignoreCase);
    }

    [return: DynamicallyAccessedMembers(Trimming.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Trimming.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumUtf8Parser<>).MakeGenericType(resultType);
}
