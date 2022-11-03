using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Parsers.Utf8;

public class EnumUtf8ParserFactory : ICsvParserFactory<byte>
{
    /// <inheritdoc cref="EnumUtf8Parser{TEnum}.AllowUndefinedValues"/>
    public bool AllowUndefinedValues { get; set; }

    /// <inheritdoc cref="EnumUtf8Parser{TEnum}.IgnoreCase"/>
    public bool IgnoreCase { get; set; }

    public EnumUtf8ParserFactory(
        bool allowUndefinedValues = false,
        bool ignoreCase = true)
    {
        AllowUndefinedValues = allowUndefinedValues;
        IgnoreCase = ignoreCase;
    }

    public bool CanParse(Type resultType)
    {
        return resultType.IsEnum && !resultType.HasAttribute<FlagsAttribute>();
    }

    public ICsvParser<byte> Create(Type resultType, CsvReaderOptions<byte> options)
    {
        return ActivatorEx.CreateInstance<ICsvParser<byte>>(
            typeof(EnumUtf8Parser<>).MakeGenericType(resultType),
            parameters: new object[] { AllowUndefinedValues, IgnoreCase });
    }
}
