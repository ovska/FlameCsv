using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class EnumUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static EnumUtf8ConverterFactory Instance { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool CanConvert(Type type)
    {
        return type.IsEnum;
    }

    public override CsvConverter<byte> Create(Type type, CsvOptions<byte> options)
    {
        return GetParserType(type).CreateInstance<CsvConverter<byte>>(options);
    }

    [return: DynamicallyAccessedMembers(Messages.Ctors)]
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = Messages.StructFactorySuppressionMessage)]
    private static Type GetParserType(Type resultType) => typeof(EnumUtf8Converter<>).MakeGenericType(resultType);
}
