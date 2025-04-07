using System.Globalization;

namespace FlameCsv.Converters;

internal static class DefaultConverters
{
    private const NumberStyles Integer = NumberStyles.Integer;
    private const NumberStyles Float = NumberStyles.Float;

    public static Func<CsvOptions<char>, CsvConverter<char>>? GetText(Type type)
    {
        if (type.IsEnum) return null;
        if (type == typeof(String))
            return o => o.StringPool is not null
                ? new PoolingStringTextConverter(o)
                : StringTextConverter.Instance;
        if (type == typeof(Int32)) return o => new NumberTextConverter<int>(o, Integer);
        if (type == typeof(Double)) return o => new NumberTextConverter<double>(o, Float);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanTextConverter(o)
                : BooleanTextConverter.Instance;
        if (type == typeof(DateTime)) return o => new SpanTextConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberTextConverter<byte>(o, Integer);
        if (type == typeof(SByte)) return o => new NumberTextConverter<sbyte>(o, Integer);
        if (type == typeof(Int16)) return o => new NumberTextConverter<short>(o, Integer);
        if (type == typeof(UInt16)) return o => new NumberTextConverter<ushort>(o, Integer);
        if (type == typeof(UInt32)) return o => new NumberTextConverter<uint>(o, Integer);
        if (type == typeof(Int64)) return o => new NumberTextConverter<long>(o, Integer);
        if (type == typeof(UInt64)) return o => new NumberTextConverter<ulong>(o, Integer);
        if (type == typeof(Single)) return o => new NumberTextConverter<float>(o, Float);
        if (type == typeof(Decimal)) return o => new NumberTextConverter<decimal>(o, Float);
        if (type == typeof(DateTimeOffset)) return o => new SpanTextConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanTextConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanTextConverter<Guid>(o);
        if (type == typeof(Char)) return _ => CharTextConverter.Instance;
        return null;
    }

    public static Func<CsvOptions<byte>, CsvConverter<byte>>? GetUtf8(Type type)
    {
        if (type.IsEnum) return null;
        if (type == typeof(String))
            return o => o.StringPool is not null
                ? new PoolingStringUtf8Converter(o)
                : StringUtf8Converter.Instance;
        if (type == typeof(Int32)) return o => new NumberUtf8Converter<int>(o, Integer);
        if (type == typeof(Double)) return o => new NumberUtf8Converter<double>(o, Float);
        if (type == typeof(Boolean))
            return o => o.HasBooleanValues
                ? new CustomBooleanUtf8Converter(o)
                : BooleanUtf8Converter.Instance;
        if (type == typeof(DateTime)) return o => new SpanUtf8FormattableConverter<DateTime>(o);
        if (type == typeof(Byte)) return o => new NumberUtf8Converter<byte>(o, Integer);
        if (type == typeof(SByte)) return o => new NumberUtf8Converter<sbyte>(o, Integer);
        if (type == typeof(Int16)) return o => new NumberUtf8Converter<short>(o, Integer);
        if (type == typeof(UInt16)) return o => new NumberUtf8Converter<ushort>(o, Integer);
        if (type == typeof(UInt32)) return o => new NumberUtf8Converter<uint>(o, Integer);
        if (type == typeof(Int64)) return o => new NumberUtf8Converter<long>(o, Integer);
        if (type == typeof(UInt64)) return o => new NumberUtf8Converter<ulong>(o, Integer);
        if (type == typeof(Single)) return o => new NumberUtf8Converter<float>(o, Float);
        if (type == typeof(Decimal)) return o => new NumberUtf8Converter<decimal>(o, Float);
        if (type == typeof(DateTimeOffset)) return o => new SpanUtf8FormattableConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan)) return o => new SpanUtf8FormattableConverter<TimeSpan>(o);
        if (type == typeof(Guid)) return o => new SpanUtf8FormattableConverter<Guid>(o);
        if (type == typeof(Char)) return _ => CharUtf8Converter.Instance;
        return null;
    }
}
