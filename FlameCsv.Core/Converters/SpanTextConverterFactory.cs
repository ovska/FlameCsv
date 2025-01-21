using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class SpanTextConverterFactory : CsvConverterFactory<char>
{
    public static readonly SpanTextConverterFactory Instance = new();

    private SpanTextConverterFactory() { }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    [SuppressMessage("Trimming", "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "<Pending>")]
    public override bool CanConvert(Type type)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            return false;

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

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        Debug.Assert(RuntimeFeature.IsDynamicCodeSupported);
        return typeof(SpanTextConverter<>).MakeGenericType(type).CreateInstance<CsvConverter<char>>(options);
    }
}
