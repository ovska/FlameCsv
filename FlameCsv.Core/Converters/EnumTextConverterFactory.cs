using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

[RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
internal sealed class EnumTextConverterFactory : CsvConverterFactory<char>
{
    public static EnumTextConverterFactory Instance { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool CanConvert(Type type)
    {
        return type.IsEnum;
    }

    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        return typeof(EnumTextConverter<>)
            .MakeGenericType(type)
            .CreateInstance<CsvConverter<char>>(options);
    }
}
