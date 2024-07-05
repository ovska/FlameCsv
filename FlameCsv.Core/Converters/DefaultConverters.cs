using System.Collections.Frozen;

namespace FlameCsv.Converters;

public static partial class DefaultConverters
{
    internal delegate CsvConverter<T> ConverterFactory<T>(CsvOptions<T> options) where T : unmanaged, IEquatable<T>;

    internal static readonly Lazy<FrozenDictionary<Type, ConverterFactory<char>>> Text = new(InitText);
    internal static readonly Lazy<FrozenDictionary<Type, ConverterFactory<byte>>> Utf8 = new(InitUtf8);

    private static FrozenDictionary<Type, ConverterFactory<char>> InitText()
    {
        List<KeyValuePair<Type, ConverterFactory<char>>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(new(typeof(string), o => o.StringPool is not null ? new PoolingStringTextConverter(o) : StringTextConverter.Instance));
        value.Add(new(typeof(bool), o => o._booleanValues is { Count: > 0 } ? new CustomBooleanTextConverter(o) : BooleanTextConverter.Instance));
        value.Add(new(typeof(DateTime), o => new SpanTextConverter<DateTime>(o)));
        value.Add(new(typeof(DateTimeOffset), o => new SpanTextConverter<DateTimeOffset>(o)));
        value.Add(new(typeof(TimeSpan), o => new SpanTextConverter<TimeSpan>(o)));
        value.Add(new(typeof(Guid), o => new SpanTextConverter<Guid>(o)));
        return value.ToFrozenDictionary(ReferenceEqualityComparer.Instance);
    }

    private static FrozenDictionary<Type, ConverterFactory<byte>> InitUtf8()
    {
        List<KeyValuePair<Type, ConverterFactory<byte>>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(new(typeof(string), o => o.StringPool is not null ? new PoolingStringUtf8Converter(o) : StringUtf8Converter.Instance));
        value.Add(new(typeof(bool), o => o._booleanValues is { Count: > 0 } ? new CustomBooleanUtf8Converter(o) : BooleanUtf8Converter.Instance));
        value.Add(new(typeof(DateTime), o => new SpanUtf8FormattableConverter<DateTime>(o)));
        value.Add(new(typeof(DateTimeOffset), o => new SpanUtf8FormattableConverter<DateTimeOffset>(o)));
        value.Add(new(typeof(TimeSpan), o => new SpanUtf8FormattableConverter<TimeSpan>(o)));
        value.Add(new(typeof(Guid), o => new SpanUtf8FormattableConverter<Guid>(o)));
        return value.ToFrozenDictionary(ReferenceEqualityComparer.Instance);
    }
}
