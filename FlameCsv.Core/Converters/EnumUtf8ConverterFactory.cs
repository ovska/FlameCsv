using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class EnumUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static EnumUtf8ConverterFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsEnum && type.GetCustomAttribute<FlagsAttribute>(inherit: false) is null;
    }

    public override CsvConverter<byte> Create(Type type, CsvOptions<byte> options)
    {
        return GetParserType(type).CreateInstance<CsvConverter<byte>>(options);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumUtf8Converter<>).MakeGenericType(resultType);
}
