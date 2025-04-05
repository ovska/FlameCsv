namespace FlameCsv.Converters;

internal static class DefaultConverters
{
    public static Func<CsvOptions<char>, CsvConverter<char>>? GetText(Type type)
    {
        if (type.IsEnum) return null;
        if (type == typeof(String))
            return o => o.StringPool is not null
                ? new PoolingStringTextConverter(o)
                : StringTextConverter.Instance;
        if (type == typeof(Int32)) return o => new NumberTextConverter<int, IntegerStyles>(o);
        if (type == typeof(Double)) return o => new NumberTextConverter<double, FloatStyles>(o);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanTextConverter(o)
                : BooleanTextConverter.Instance;
        if (type == typeof(DateTime)) return o => new SpanTextConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberTextConverter<byte, IntegerStyles>(o);
        if (type == typeof(SByte)) return o => new NumberTextConverter<sbyte, IntegerStyles>(o);
        if (type == typeof(Int16)) return o => new NumberTextConverter<short, IntegerStyles>(o);
        if (type == typeof(UInt16)) return o => new NumberTextConverter<ushort, IntegerStyles>(o);
        if (type == typeof(UInt32)) return o => new NumberTextConverter<uint, IntegerStyles>(o);
        if (type == typeof(Int64)) return o => new NumberTextConverter<long, IntegerStyles>(o);
        if (type == typeof(UInt64)) return o => new NumberTextConverter<ulong, IntegerStyles>(o);
        if (type == typeof(Single)) return o => new NumberTextConverter<float, FloatStyles>(o);
        if (type == typeof(Decimal)) return o => new NumberTextConverter<decimal, FloatStyles>(o);
        if (type == typeof(DateTimeOffset)) return o => new SpanTextConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanTextConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanTextConverter<Guid>(o);
        if (type == typeof(Char)) return _ => CharTextConverter.Instance;
        if (type == typeof(Memory<byte>)) return _ => Base64TextConverter.Instance;
        return null;
    }

    public static Func<CsvOptions<byte>, CsvConverter<byte>>? GetUtf8(Type type)
    {
        if (type.IsEnum) return null;
        if (type == typeof(String))
            return o => o.StringPool is not null
                ? new PoolingStringUtf8Converter(o)
                : StringUtf8Converter.Instance;
        if (type == typeof(Int32)) return o => new NumberUtf8Converter<int, IntegerStyles>(o);
        if (type == typeof(Double)) return o => new NumberUtf8Converter<double, FloatStyles>(o);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanUtf8Converter(o)
                : BooleanUtf8Converter.Instance;
        if (type == typeof(DateTime)) return o => new SpanUtf8FormattableConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberUtf8Converter<byte, IntegerStyles>(o);
        if (type == typeof(SByte)) return o => new NumberUtf8Converter<sbyte, IntegerStyles>(o);
        if (type == typeof(Int16)) return o => new NumberUtf8Converter<short, IntegerStyles>(o);
        if (type == typeof(UInt16)) return o => new NumberUtf8Converter<ushort, IntegerStyles>(o);
        if (type == typeof(UInt32)) return o => new NumberUtf8Converter<uint, IntegerStyles>(o);
        if (type == typeof(Int64)) return o => new NumberUtf8Converter<long, IntegerStyles>(o);
        if (type == typeof(UInt64)) return o => new NumberUtf8Converter<ulong, IntegerStyles>(o);
        if (type == typeof(Single)) return o => new NumberUtf8Converter<float, FloatStyles>(o);
        if (type == typeof(Decimal)) return o => new NumberUtf8Converter<decimal, FloatStyles>(o);
        if (type == typeof(DateTimeOffset)) return o => new SpanUtf8FormattableConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanUtf8FormattableConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanUtf8FormattableConverter<Guid>(o);
        if (type == typeof(Char)) return _ => CharUtf8Converter.Instance;
        if (type == typeof(Memory<byte>)) return _ => Base64Utf8Converter.Instance;
        return null;
    }
}
