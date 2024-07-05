using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class SpanTextConverterFactory : CsvConverterFactory<char>
{
    public static readonly SpanTextConverterFactory Instance = new SpanTextConverterFactory();

    private SpanTextConverterFactory() { }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    public override bool CanConvert(Type type)
        => RuntimeFeature.IsDynamicCodeSupported
        && type.IsAssignableTo(typeof(ISpanFormattable))
        && type.IsAssignableTo(typeof(ISpanParsable<>).MakeGenericType(type));

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Guarded with RuntimeFeature.IsDynamicCodeSupported")]
    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        Debug.Assert(RuntimeFeature.IsDynamicCodeSupported);
        return typeof(SpanTextConverter<>).MakeGenericType(type).CreateInstance<CsvConverter<char>>(options);
    }
}
