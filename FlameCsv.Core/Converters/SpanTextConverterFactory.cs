using FlameCsv.Extensions;

namespace FlameCsv.Converters;

[RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
internal sealed class SpanTextConverterFactory : CsvConverterFactory<char>
{
    public static readonly SpanTextConverterFactory Instance = new();

    private SpanTextConverterFactory() { }

    public override bool CanConvert(Type type)
    {
        bool formattable = false;
        bool parsable = false;

        foreach (var iface in type.GetInterfaces())
        {
            if (iface == typeof(ISpanFormattable))
            {
                formattable = true;
            }
            else if (iface.IsGenericType
                     && iface.GetGenericTypeDefinition() == typeof(ISpanParsable<>)
                     && iface.GetGenericArguments()[0] == type)
            {
                parsable = true;
            }
        }

        return formattable && parsable;
    }

    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        return typeof(SpanTextConverter<>).MakeGenericType(type).CreateInstance<CsvConverter<char>>(options);
    }
}
