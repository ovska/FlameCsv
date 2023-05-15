namespace FlameCsv.Converters;

internal static partial class DefaultConverters
{
    public delegate CsvConverter<char> TextConverterFactory(CsvTextOptions options);
    public delegate CsvConverter<byte> Utf8ConverterFactory(CsvUtf8Options options);

    public static Dictionary<Type, TextConverterFactory> Text => _defaultText ?? InitText();
    public static Dictionary<Type, Utf8ConverterFactory> Utf8 => _defaultUtf8 ?? InitUtf8();

    private static Dictionary<Type, TextConverterFactory>? _defaultText;
    private static Dictionary<Type, Utf8ConverterFactory>? _defaultUtf8;

    private static Dictionary<Type, TextConverterFactory> InitText()
    {
        Dictionary<Type, TextConverterFactory> value = new(32);
        RegisterNumberConverters(value);
        value.Add(typeof(string), o => o.StringPool is { } pool ? new PoolingStringTextConverter(pool) : StringTextConverter.Instance);
        value.Add(typeof(bool), o => o._booleanValues is { Count: > 0 } bv ? new CustomBooleanTextConverter(bv) : BooleanTextConverter.Instance);
        value.Add(typeof(DateTime), o => new DateTimeTextConverter(o));
        value.Add(typeof(DateTimeOffset), o => new DateTimeOffsetTextConverter(o));
        value.Add(typeof(TimeSpan), o => new TimeSpanTextConverter(o));
        value.Add(typeof(Guid), o => new GuidTextConverter(o));
        value.TrimExcess();
        return Interlocked.CompareExchange(ref _defaultText, value, null) ?? value;
    }

    private static Dictionary<Type, Utf8ConverterFactory> InitUtf8()
    {
        Dictionary<Type, Utf8ConverterFactory> value = new(32);
        RegisterNumberConverters(value);
        value.Add(typeof(string), o => o.StringPool is { } pool ? new PoolingStringUtf8Converter(pool) : StringUtf8Converter.Instance);
        value.Add(typeof(bool), o => o._booleanValues is { Count: > 0 } bv ? new CustomBooleanUtf8Converter(bv) : new BooleanUtf8Converter(o.BooleanFormat));
        value.Add(typeof(DateTime), o => new DateTimeUtf8Converter(o));
        value.Add(typeof(DateTimeOffset), o => new DateTimeOffsetUtf8Converter(o));
        value.Add(typeof(TimeSpan), o => new TimeSpanUtf8Converter(o));
        value.Add(typeof(Guid), o => new GuidUtf8Converter(o.GuidFormat));
        value.TrimExcess();
        return Interlocked.CompareExchange(ref _defaultUtf8, value, null) ?? value;
    }
}
