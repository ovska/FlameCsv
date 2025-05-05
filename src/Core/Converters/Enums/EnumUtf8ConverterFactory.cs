using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Enums;

[RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
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
        return typeof(EnumUtf8Converter<>)
            .MakeGenericType(type)
            .CreateInstance<CsvConverter<byte>>(options);
    }
}
