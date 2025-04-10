using System.Globalization;

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
        if (type == typeof(Int32)) return o => new NumberTextConverter<int>(o, NumberStyles.Integer);
        if (type == typeof(Double)) return o => new NumberTextConverter<double>(o, NumberStyles.Float);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanTextConverter(o)
                : BooleanTextConverter.Instance;
        if (type == typeof(DateTime)) return o => new SpanTextConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberTextConverter<byte>(o, NumberStyles.Integer);
        if (type == typeof(SByte)) return o => new NumberTextConverter<sbyte>(o, NumberStyles.Integer);
        if (type == typeof(Int16)) return o => new NumberTextConverter<short>(o, NumberStyles.Integer);
        if (type == typeof(UInt16)) return o => new NumberTextConverter<ushort>(o, NumberStyles.Integer);
        if (type == typeof(UInt32)) return o => new NumberTextConverter<uint>(o, NumberStyles.Integer);
        if (type == typeof(Int64)) return o => new NumberTextConverter<long>(o, NumberStyles.Integer);
        if (type == typeof(UInt64)) return o => new NumberTextConverter<ulong>(o, NumberStyles.Integer);
        if (type == typeof(Single)) return o => new NumberTextConverter<float>(o, NumberStyles.Float);
        if (type == typeof(Decimal)) return o => new NumberTextConverter<decimal>(o, NumberStyles.Float);
        if (type == typeof(DateTimeOffset)) return o => new SpanTextConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanTextConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanTextConverter<Guid>(o);
        if (type == typeof(Char)) return o => new SpanTextConverter<char>(o);
        return null;
    }

    public static Func<CsvOptions<byte>, CsvConverter<byte>>? GetUtf8(Type type)
    {
        if (type.IsEnum) return null;
        if (type == typeof(String))
            return o => o.StringPool is not null
                ? new PoolingStringUtf8Converter(o)
                : StringUtf8Converter.Instance;
        if (type == typeof(Int32)) return o => new NumberUtf8Converter<int>(o, NumberStyles.Integer);
        if (type == typeof(Double)) return o => new NumberUtf8Converter<double>(o, NumberStyles.Float);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanUtf8Converter(o)
                : BooleanUtf8Converter.Instance;
        if (type == typeof(DateTime)) return o => new SpanUtf8FormattableConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberUtf8Converter<byte>(o, NumberStyles.Integer);
        if (type == typeof(SByte)) return o => new NumberUtf8Converter<sbyte>(o, NumberStyles.Integer);
        if (type == typeof(Int16)) return o => new NumberUtf8Converter<short>(o, NumberStyles.Integer);
        if (type == typeof(UInt16)) return o => new NumberUtf8Converter<ushort>(o, NumberStyles.Integer);
        if (type == typeof(UInt32)) return o => new NumberUtf8Converter<uint>(o, NumberStyles.Integer);
        if (type == typeof(Int64)) return o => new NumberUtf8Converter<long>(o, NumberStyles.Integer);
        if (type == typeof(UInt64)) return o => new NumberUtf8Converter<ulong>(o, NumberStyles.Integer);
        if (type == typeof(Single)) return o => new NumberUtf8Converter<float>(o, NumberStyles.Float);
        if (type == typeof(Decimal)) return o => new NumberUtf8Converter<decimal>(o, NumberStyles.Float);
        if (type == typeof(DateTimeOffset)) return o => new SpanUtf8FormattableConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanUtf8FormattableConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanUtf8FormattableConverter<Guid>(o);
        if (type == typeof(Char)) return o => new SpanUtf8Converter<char>(o);
        return null;
    }
}
