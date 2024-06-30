using System.Collections.Frozen;

namespace FlameCsv.Converters;

public static partial class DefaultConverters
{
    internal delegate CsvConverter<char> TextConverterFactory(CsvTextOptions options);
    internal delegate CsvConverter<byte> Utf8ConverterFactory(CsvUtf8Options options);

    internal static readonly Lazy<FrozenDictionary<Type, TextConverterFactory>> Text = new(InitText);
    internal static readonly Lazy<FrozenDictionary<Type, Utf8ConverterFactory>> Utf8 = new(InitUtf8);

    private static FrozenDictionary<Type, TextConverterFactory> InitText()
    {
        List<KeyValuePair<Type, TextConverterFactory>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(new(typeof(string), o => o.StringPool is not null ? new PoolingStringTextConverter(o) : new StringTextConverter(o)));
        value.Add(new(typeof(bool), o => o._booleanValues is { Count: > 0 } bv ? new CustomBooleanTextConverter(bv) : BooleanTextConverter.Instance));
        value.Add(new(typeof(DateTime), o => new DateTimeTextConverter(o)));
        value.Add(new(typeof(DateTimeOffset), o => new DateTimeOffsetTextConverter(o)));
        value.Add(new(typeof(TimeSpan), o => new TimeSpanTextConverter(o)));
        value.Add(new(typeof(Guid), o => new GuidTextConverter(o)));
        return value.ToFrozenDictionary();
    }

    private static FrozenDictionary<Type, Utf8ConverterFactory> InitUtf8()
    {
        List<KeyValuePair<Type, Utf8ConverterFactory>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(new(typeof(string), o => o.StringPool is not null ? new PoolingStringUtf8Converter(o) : new StringUtf8Converter(o)));
        value.Add(new(typeof(bool), o => o._booleanValues is { Count: > 0 } bv ? new CustomBooleanUtf8Converter(bv) : new BooleanUtf8Converter(o.BooleanFormat)));
        value.Add(new(typeof(DateTime), o => new DateTimeUtf8Converter(o.DateTimeFormat)));
        value.Add(new(typeof(DateTimeOffset), o => new DateTimeOffsetUtf8Converter(o.DateTimeFormat)));
        value.Add(new(typeof(TimeSpan), o => new TimeSpanUtf8Converter(o.TimeSpanFormat)));
        value.Add(new(typeof(Guid), o => new GuidUtf8Converter(o.GuidFormat)));
        return value.ToFrozenDictionary();
    }
}
