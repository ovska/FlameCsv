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

    public override bool CanConvert(Type type)
    {
        return RuntimeFeature.IsDynamicCodeSupported && CheckInterfaces(type) is not Implements.None;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
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
    private static Implements CheckInterfaces(Type type)
    {
        bool formattable = type.IsAssignableTo(typeof(IUtf8SpanFormattable));

        // needs to be ISpanFormattable if not Utf8 formattable
        if (!formattable && !type.IsAssignableTo(typeof(ISpanFormattable)))
        {
            return Implements.None;
        }

        bool parsable = type.IsAssignableTo(typeof(IUtf8SpanParsable<>).MakeGenericType(type));

        // needs to be ISpanParsable if not utf8 parsable
        if (!parsable && !type.IsAssignableTo(typeof(ISpanParsable<>).MakeGenericType(type)))
        {
            return Implements.None;
        }

        return (formattable, parsable) switch
        {
            (false, false) => Implements.None,
            (true, false) => Implements.Formattable,
            (false, true) => Implements.Parsable,
            (true, true) => Implements.Both,
        };
    }

    private enum Implements { None, Formattable, Parsable, Both }
}
