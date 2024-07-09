using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class SpanUtf8ConverterFactory : CsvConverterFactory<byte>
{
    public static readonly SpanUtf8ConverterFactory Instance = new SpanUtf8ConverterFactory();

    private SpanUtf8ConverterFactory() { }

    [SuppressMessage("Trimming", "IL2067:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "<Pending>")]
    public override bool CanConvert(Type type)
    {
        return RuntimeFeature.IsDynamicCodeSupported && CheckInterfaces(type) is not Implements.None;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    [SuppressMessage("Trimming", "IL2067:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "<Pending>")]
    public override CsvConverter<byte> Create(Type type, CsvOptions<byte> options)
    {
        Debug.Assert(RuntimeFeature.IsDynamicCodeSupported);

        Type toCreate = CheckInterfaces(type) switch
        {
            Implements.Both => typeof(SpanUtf8Converter<>),
            Implements.Formattable => typeof(SpanUtf8FormattableConverter<>),
            Implements.Parsable => typeof(SpanUtf8ParsableConverter<>),
            _ => throw new InvalidOperationException($"{GetType().ToTypeString()}.Create called with invalid type: {type.ToTypeString()}"),
        };

        return toCreate.MakeGenericType(type).CreateInstance<CsvConverter<byte>>(options);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    private static Implements CheckInterfaces([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
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
