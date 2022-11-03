using FlameCsv.Extensions;
using FlameCsv.Runtime;

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
        return resultType.IsEnum && !resultType.HasAttribute<FlagsAttribute>();
    }

    public ICsvParser<char> Create(Type resultType, CsvReaderOptions<char> options)
    {
        return ActivatorEx.CreateInstance<ICsvParser<char>>(
            typeof(EnumTextParser<>).MakeGenericType(resultType),
            parameters: new object[] { AllowUndefinedValues, IgnoreCase });
    }
}
