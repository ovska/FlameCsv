using System.Collections.Frozen;
using System.Globalization;
using FlameCsv.Converters.Formattable;

namespace FlameCsv.Converters;

// csharpier-ignore
internal static class DefaultConverters
{
    // rare oddities such as sbyte, Half, nuint, nint are not included here on purpose
    // the span converter factory will create them if needed.
    // common types are included for better performance (avoiding reflection and generic instantiation at runtime)

    public static Func<CsvOptions<char>, CsvConverter<char>>? GetText(Type type)
    {
        if (TextConverters.TryGetValue(type.MetadataToken, out var converterFactory))
        {
            return converterFactory;
        }

        return null;
    }

    public static Func<CsvOptions<byte>, CsvConverter<byte>>? GetUtf8(Type type)
    {
        if (Utf8Converters.TryGetValue(type.MetadataToken, out var converterFactory))
        {
            return converterFactory;
        }

        return null;
    }

    private static FrozenDictionary<int, Func<CsvOptions<char>, CsvConverter<char>>> TextConverters { get; } =
        new Dictionary<int, Func<CsvOptions<char>, CsvConverter<char>>>()
        {
            [typeof(string).MetadataToken] = _ => StringTextConverter.Instance,
            [typeof(bool).MetadataToken] = o =>
                o.HasBooleanValues ? new CustomBooleanConverter<char>(o) : BooleanTextConverter.Instance,
            [typeof(char).MetadataToken] = o => new SpanTextConverter<char>(o),
            [typeof(short).MetadataToken] = o => new NumberTextConverter<short>(o, NumberStyles.Integer),
            [typeof(int).MetadataToken] = o => new NumberTextConverter<int>(o, NumberStyles.Integer),
            [typeof(long).MetadataToken] = o => new NumberTextConverter<long>(o, NumberStyles.Integer),
            [typeof(byte).MetadataToken] = o => new NumberTextConverter<byte>(o, NumberStyles.Integer),
            [typeof(ushort).MetadataToken] = o => new NumberTextConverter<ushort>(o, NumberStyles.Integer),
            [typeof(uint).MetadataToken] = o => new NumberTextConverter<uint>(o, NumberStyles.Integer),
            [typeof(ulong).MetadataToken] = o => new NumberTextConverter<ulong>(o, NumberStyles.Integer),
            [typeof(float).MetadataToken] = o => new NumberTextConverter<float>(o, NumberStyles.Float),
            [typeof(double).MetadataToken] = o => new NumberTextConverter<double>(o, NumberStyles.Float),
            [typeof(decimal).MetadataToken] = o => new NumberTextConverter<decimal>(o, NumberStyles.Float),
            [typeof(Guid).MetadataToken] = o => new SpanTextConverter<Guid>(o),
            [typeof(TimeSpan).MetadataToken] = o => new SpanTextConverter<TimeSpan>(o),
            [typeof(DateTime).MetadataToken] = o => new SpanTextConverter<DateTime>(o),
            [typeof(DateTimeOffset).MetadataToken] = o => new SpanTextConverter<DateTimeOffset>(o),
            [typeof(byte[]).MetadataToken] = _ => Base64TextConverter.Array,
            [typeof(Memory<byte>).MetadataToken] = _ => Base64TextConverter.Memory,
            [typeof(ArraySegment<byte>).MetadataToken] = _ => Base64TextConverter.Instance,
            [typeof(ReadOnlyMemory<byte>).MetadataToken] = _ => Base64TextConverter.ReadOnlyMemory,
        }.ToFrozenDictionary();

    private static FrozenDictionary<int, Func<CsvOptions<byte>, CsvConverter<byte>>> Utf8Converters { get; } =
        new Dictionary<int, Func<CsvOptions<byte>, CsvConverter<byte>>>()
        {
            [typeof(string).MetadataToken] = _ => StringUtf8Converter.Instance,
            [typeof(bool).MetadataToken] = o =>
                o.HasBooleanValues ? new CustomBooleanConverter<byte>(o) : BooleanUtf8Converter.Instance,
            [typeof(char).MetadataToken] = o => new SpanUtf8Converter<char>(o),
            [typeof(short).MetadataToken] = o => new NumberUtf8Converter<short>(o, NumberStyles.Integer),
            [typeof(int).MetadataToken] = o => new NumberUtf8Converter<int>(o, NumberStyles.Integer),
            [typeof(long).MetadataToken] = o => new NumberUtf8Converter<long>(o, NumberStyles.Integer),
            [typeof(byte).MetadataToken] = o => new NumberUtf8Converter<byte>(o, NumberStyles.Integer),
            [typeof(ushort).MetadataToken] = o => new NumberUtf8Converter<ushort>(o, NumberStyles.Integer),
            [typeof(uint).MetadataToken] = o => new NumberUtf8Converter<uint>(o, NumberStyles.Integer),
            [typeof(ulong).MetadataToken] = o => new NumberUtf8Converter<ulong>(o, NumberStyles.Integer),
            [typeof(float).MetadataToken] = o => new NumberUtf8Converter<float>(o, NumberStyles.Float),
            [typeof(double).MetadataToken] = o => new NumberUtf8Converter<double>(o, NumberStyles.Float),
            [typeof(decimal).MetadataToken] = o => new NumberUtf8Converter<decimal>(o, NumberStyles.Float),
            [typeof(Guid).MetadataToken] = o => new SpanUtf8FormattableConverter<Guid>(o),
            [typeof(TimeSpan).MetadataToken] = o => new SpanUtf8FormattableConverter<TimeSpan>(o),
            [typeof(DateTime).MetadataToken] = o => new SpanUtf8FormattableConverter<DateTime>(o),
            [typeof(DateTimeOffset).MetadataToken] = o => new SpanUtf8FormattableConverter<DateTimeOffset>(o),
            [typeof(byte[]).MetadataToken] = _ => Base64Utf8Converter.Array,
            [typeof(Memory<byte>).MetadataToken] = _ => Base64Utf8Converter.Memory,
            [typeof(ArraySegment<byte>).MetadataToken] = _ => Base64Utf8Converter.Instance,
            [typeof(ReadOnlyMemory<byte>).MetadataToken] = _ => Base64Utf8Converter.ReadOnlyMemory,
        }.ToFrozenDictionary();
}
