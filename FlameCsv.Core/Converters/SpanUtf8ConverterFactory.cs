using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

[RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
internal sealed class SpanUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static readonly SpanUtf8ConverterFactory Instance = new();

    private SpanUtf8ConverterFactory() { }

    public override bool CanConvert(Type type)
    {
        return RuntimeFeature.IsDynamicCodeSupported && CheckInterfaces(type) is not Implements.None;
    }

    public override CsvConverter<byte> Create(Type type, CsvOptions<byte> options)
    {
        Debug.Assert(RuntimeFeature.IsDynamicCodeSupported);

        Type toCreate = CheckInterfaces(type) switch
        {
            Implements.Both => typeof(SpanUtf8Converter<>),
            Implements.Formattable => typeof(SpanUtf8FormattableConverter<>),
            Implements.Parsable => typeof(SpanUtf8ParsableConverter<>),
            _ => throw new InvalidOperationException($"{GetType().FullName}.Create called with invalid type: {type.FullName}"),
        };

        return toCreate.MakeGenericType(type).CreateInstance<CsvConverter<byte>>(options);
    }

    private static Implements CheckInterfaces(Type type)
    {
        bool? formattable = null;
        bool? parsable = null;

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType)
            {
                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(IUtf8SpanParsable<>))
                {
                    parsable = iface.GetGenericArguments()[0] == type;
                }
                else if (def == typeof(ISpanParsable<>))
                {
                    parsable = iface.GetGenericArguments()[0] == type;
                }
            }
            else
            {
                if (iface == typeof(IUtf8SpanFormattable))
                {
                    formattable = true;
                }
                else if (iface == typeof(ISpanFormattable))
                {
                    formattable ??= false;
                }
            }
        }

        return (formattable, parsable) switch
        {
            (true, true) => Implements.Both,
            (true, false) => Implements.Formattable,
            (false, true) => Implements.Parsable,
            _ => Implements.None,
        };
    }

    private enum Implements { None, Formattable, Parsable, Both }
}
