using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Formattable;

[RUF(Messages.ConverterFactories)]
internal sealed class SpanUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static readonly SpanUtf8ConverterFactory Instance = new();

    private SpanUtf8ConverterFactory()
    {
    }

    public override bool CanConvert(Type type)
    {
        return CheckInterfaces(type) is not Implements.None;
    }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public override CsvConverter<byte> Create(Type type, CsvOptions<byte> options)
    {
        var implements = CheckInterfaces(type);

        Type toCreate = implements switch
        {
            Implements.Both => typeof(SpanUtf8Converter<>),
            Implements.Formattable => typeof(SpanUtf8FormattableConverter<>),
            Implements.Parsable => typeof(SpanUtf8ParsableConverter<>),
            Implements.Transcoding => typeof(SpanUtf8TranscodingConverter<>),
            Implements.Integer or Implements.Float => typeof(NumberUtf8Converter<>),
            _ => throw new ArgumentException("Invalid type", nameof(type)),
        };

        object?[] parameters = implements switch
        {
            Implements.Integer => [options, NumberStyles.Integer],
            Implements.Float => [options, NumberStyles.Float],
            _ => [options],
        };

        return toCreate.MakeGenericType(type).CreateInstance<CsvConverter<byte>>(parameters);
    }

    private static Implements CheckInterfaces(Type type)
    {
        // hack, we don't want to treat char as a number
        if (type == typeof(char)) return Implements.Both;

        InterfaceType? formattable = null;
        InterfaceType? parsable = null;

        bool? isFloatingPoint = null;

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.Module != typeof(int).Module) continue;

            if (iface.IsGenericType)
            {
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(IUtf8SpanParsable<>))
                {
                    if (iface.GetGenericArguments()[0] == type)
                    {
                        parsable = InterfaceType.Byte;
                    }
                }
                else if (def == typeof(ISpanParsable<>))
                {
                    if (iface.GetGenericArguments()[0] == type)
                    {
                        parsable ??= InterfaceType.Char;
                    }
                }
                else if (def == typeof(IBinaryInteger<>))
                {
                    isFloatingPoint = false;
                }
                else if (def == typeof(IFloatingPoint<>))
                {
                    isFloatingPoint = true;
                }
            }
            else
            {
                if (iface == typeof(IUtf8SpanFormattable))
                {
                    formattable = InterfaceType.Byte;
                }
                else if (iface == typeof(ISpanFormattable))
                {
                    formattable ??= InterfaceType.Char;
                }
            }
        }

        // ReSharper disable once ConvertSwitchStatementToSwitchExpression
        switch (isFloatingPoint)
        {
            case true: return Implements.Float;
            case false: return Implements.Integer;
        }

        return (formattable, parsable) switch
        {
            (formattable: InterfaceType.Byte, parsable: InterfaceType.Byte) => Implements.Both,
            (formattable: InterfaceType.Byte, parsable: InterfaceType.Char) => Implements.Formattable,
            (formattable: InterfaceType.Char, parsable: InterfaceType.Byte) => Implements.Parsable,
            (formattable: InterfaceType.Char, parsable: InterfaceType.Char) => Implements.Transcoding,
            _ => Implements.None,
        };
    }

    private enum InterfaceType : byte
    {
        Char,
        Byte
    }

    private enum Implements : byte
    {
        None,
        Transcoding,
        Formattable,
        Parsable,
        Both,
        Integer,
        Float,
    }
}
