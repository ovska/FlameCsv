using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Text;

/// <summary>
/// Parser factory for non-flags enums.
/// </summary>
internal sealed class EnumTextConverterFactory : CsvConverterFactory<char>
{
    public static EnumTextConverterFactory Instance { get; } = new();

    public override bool CanConvert(Type resultType)
    {
        return resultType.IsEnum && resultType.GetCustomAttribute<FlagsAttribute>(inherit: false) is null;
    }

    public override CsvConverter<char> Create(Type resultType, CsvOptions<char> options)
    {
        return GetParserType(resultType).CreateInstance<CsvConverter<char>>(options as CsvTextOptions);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumTextConverter<>).MakeGenericType(resultType);
}
