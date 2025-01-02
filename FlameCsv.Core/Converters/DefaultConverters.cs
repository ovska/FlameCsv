using System.Collections.Frozen;

namespace FlameCsv.Converters;

public static partial class DefaultConverters
{
    internal delegate CsvConverter<T> ConverterFactory<T>(CsvOptions<T> options) where T : unmanaged, IEquatable<T>;

    internal static readonly Lazy<FrozenDictionary<Type, ConverterFactory<char>>> Text = new(InitText);
    internal static readonly Lazy<FrozenDictionary<Type, ConverterFactory<byte>>> Utf8 = new(InitUtf8);

    private static FrozenDictionary<Type, ConverterFactory<char>> InitText()
    {
        Dictionary<Type, ConverterFactory<char>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(typeof(string), o => o.StringPool is not null ? new PoolingStringTextConverter(o) : StringTextConverter.Instance);
        value.Add(typeof(bool), o => o._booleanValues is { Count: > 0 } ? new CustomBooleanTextConverter(o) : BooleanTextConverter.Instance);
        value.Add(typeof(DateTime), o => new SpanTextConverter<DateTime>(o));
        value.Add(typeof(DateTimeOffset), o => new SpanTextConverter<DateTimeOffset>(o));
        value.Add(typeof(TimeSpan), o => new SpanTextConverter<TimeSpan>(o));
        value.Add(typeof(Guid), o => new SpanTextConverter<Guid>(o));
        return value.ToFrozenDictionary();
    }

    private static FrozenDictionary<Type, ConverterFactory<byte>> InitUtf8()
    {
        Dictionary<Type, ConverterFactory<byte>> value = new(capacity: 32);
        RegisterNumberConverters(value);
        value.Add(typeof(string), o => o.StringPool is not null ? new PoolingStringUtf8Converter(o) : StringUtf8Converter.Instance);
        value.Add(typeof(bool), o => o._booleanValues is { Count: > 0 } ? new CustomBooleanUtf8Converter(o) : BooleanUtf8Converter.Instance);
        value.Add(typeof(DateTime), o => new SpanUtf8FormattableConverter<DateTime>(o));
        value.Add(typeof(DateTimeOffset), o => new SpanUtf8FormattableConverter<DateTimeOffset>(o));
        value.Add(typeof(TimeSpan), o => new SpanUtf8FormattableConverter<TimeSpan>(o));
        value.Add(typeof(Guid), o => new SpanUtf8FormattableConverter<Guid>(o));
        return value.ToFrozenDictionary();
    }
}
