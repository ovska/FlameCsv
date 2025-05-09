using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Formattable;

[RUF(Messages.ConverterFactories)]
internal sealed class SpanTextConverterFactory : CsvConverterFactory<char>
{
    public static readonly SpanTextConverterFactory Instance = new();

    private SpanTextConverterFactory() { }

    public override bool CanConvert(Type type) => CanConvertCore(type, out _);

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        if (!CanConvertCore(type, out bool? isFloatingPoint))
            throw new NotSupportedException("The type is not supported.");

        Type toCreate = isFloatingPoint.HasValue ? typeof(NumberTextConverter<>) : typeof(SpanTextConverter<>);

        object?[] parameters = isFloatingPoint switch
        {
            true => [options, NumberStyles.Float],
            false => [options, NumberStyles.Integer],
            null => [options],
        };

        return toCreate.MakeGenericType(type).CreateInstance<CsvConverter<char>>(parameters);
    }

    private static bool CanConvertCore(Type type, out bool? isFloatingPoint)
    {
        bool formattable = false;
        bool parsable = false;
        isFloatingPoint = null;

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.Module != typeof(int).Module)
                continue;

            if (iface.IsGenericType)
            {
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(ISpanParsable<>))
                {
                    parsable |= iface.GetGenericArguments()[0] == type;
                }
                if (def == typeof(IBinaryInteger<>))
                {
                    if (iface.GetGenericArguments()[0] == type)
                    {
                        isFloatingPoint = false;
                    }
                }
                else if (def == typeof(IFloatingPoint<>))
                {
                    if (iface.GetGenericArguments()[0] == type)
                    {
                        isFloatingPoint = true;
                    }
                }
            }
            else if (iface == typeof(ISpanFormattable))
            {
                formattable = true;
            }
        }

        if (type == typeof(char))
        {
            // hack: we don't want to treat char as a number
            isFloatingPoint = null;
        }

        return formattable && parsable;
    }
}
