using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class EnumUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static EnumUtf8ConverterFactory Instance { get; } = new();

    public override bool CanConvert(Type resultType)
    {
        return resultType.IsEnum && resultType.GetCustomAttribute<FlagsAttribute>(inherit: false) is null;
    }

    public override CsvConverter<byte> Create(Type resultType, CsvOptions<byte> options)
    {
        return GetParserType(resultType).CreateInstance<CsvConverter<byte>>(options as CsvUtf8Options);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumUtf8Converter<>).MakeGenericType(resultType);
}
