using System.Globalization;
using FlameCsv.Converters.Formattable;

namespace FlameCsv.Converters;

// csharpier-ignore
internal static class DefaultConverters
{
    // rare oddities such as sbyte, Half, nuint, nint are not included here on purpose
    // the span converter factory will create them if needed,

    public static Func<CsvOptions<char>, CsvConverter<char>>? GetText(Type type)
    {
        // Early exit for non-core library types, and all enums
        if (type.IsEnum || type.Module != typeof(int).Module)
            return null;

        if (type == typeof(string))
            return _ => StringTextConverter.Instance;
        if (type == typeof(int))
            return o => new NumberTextConverter<int>(o, NumberStyles.Integer);
        if (type == typeof(double))
            return o => new NumberTextConverter<double>(o, NumberStyles.Float);
        if (type == typeof(bool))
            return o => o.HasBooleanValues ? new CustomBooleanConverter<char>(o) : BooleanTextConverter.Instance;
        if (type == typeof(DateTime))
            return o => new SpanTextConverter<DateTime>(o);
        if (type == typeof(byte))
            return o => new NumberTextConverter<byte>(o, NumberStyles.Integer);
        if (type == typeof(short))
            return o => new NumberTextConverter<short>(o, NumberStyles.Integer);
        if (type == typeof(ushort))
            return o => new NumberTextConverter<ushort>(o, NumberStyles.Integer);
        if (type == typeof(uint))
            return o => new NumberTextConverter<uint>(o, NumberStyles.Integer);
        if (type == typeof(long))
            return o => new NumberTextConverter<long>(o, NumberStyles.Integer);
        if (type == typeof(ulong))
            return o => new NumberTextConverter<ulong>(o, NumberStyles.Integer);
        if (type == typeof(float))
            return o => new NumberTextConverter<float>(o, NumberStyles.Float);
        if (type == typeof(decimal))
            return o => new NumberTextConverter<decimal>(o, NumberStyles.Float);
        if (type == typeof(DateTimeOffset))
            return o => new SpanTextConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan))
            return o => new SpanTextConverter<TimeSpan>(o);
        if (type == typeof(Guid))
            return o => new SpanTextConverter<Guid>(o);
        if (type == typeof(char))
            return o => new SpanTextConverter<char>(o);
        if (type == typeof(ArraySegment<byte>))
            return _ => Base64TextConverter.Instance;
        if (type == typeof(Memory<byte>))
            return _ => Base64TextConverter.Memory;
        if (type == typeof(ReadOnlyMemory<byte>))
            return _ => Base64TextConverter.ReadOnlyMemory;
        if (type == typeof(Memory<byte>))
            return _ => Base64TextConverter.Memory;
        if (type == typeof(byte[]))
            return _ => Base64TextConverter.Array;
        return null;
    }

    public static Func<CsvOptions<byte>, CsvConverter<byte>>? GetUtf8(Type type)
    {
        // Early exit for non-core library types, and all enums
        if (type.IsEnum || type.Module != typeof(int).Module)
            return null;

        if (type == typeof(string))
            return _ => StringUtf8Converter.Instance;
        if (type == typeof(int))
            return o => new NumberUtf8Converter<int>(o, NumberStyles.Integer);
        if (type == typeof(double))
            return o => new NumberUtf8Converter<double>(o, NumberStyles.Float);
        if (type == typeof(bool))
            return o => o.HasBooleanValues ? new CustomBooleanConverter<byte>(o) : BooleanUtf8Converter.Instance;
        if (type == typeof(DateTime))
            return o => new SpanUtf8FormattableConverter<DateTime>(o);
        if (type == typeof(byte))
            return o => new NumberUtf8Converter<byte>(o, NumberStyles.Integer);
        if (type == typeof(short))
            return o => new NumberUtf8Converter<short>(o, NumberStyles.Integer);
        if (type == typeof(ushort))
            return o => new NumberUtf8Converter<ushort>(o, NumberStyles.Integer);
        if (type == typeof(uint))
            return o => new NumberUtf8Converter<uint>(o, NumberStyles.Integer);
        if (type == typeof(long))
            return o => new NumberUtf8Converter<long>(o, NumberStyles.Integer);
        if (type == typeof(ulong))
            return o => new NumberUtf8Converter<ulong>(o, NumberStyles.Integer);
        if (type == typeof(float))
            return o => new NumberUtf8Converter<float>(o, NumberStyles.Float);
        if (type == typeof(decimal))
            return o => new NumberUtf8Converter<decimal>(o, NumberStyles.Float);
        if (type == typeof(DateTimeOffset))
            return o => new SpanUtf8FormattableConverter<DateTimeOffset>(o);
        if (type == typeof(TimeSpan))
            return o => new SpanUtf8FormattableConverter<TimeSpan>(o);
        if (type == typeof(Guid))
            return o => new SpanUtf8FormattableConverter<Guid>(o);
        if (type == typeof(char))
            return o => new SpanUtf8Converter<char>(o);
        if (type == typeof(ArraySegment<byte>))
            return _ => Base64Utf8Converter.Instance;
        if (type == typeof(Memory<byte>))
            return _ => Base64Utf8Converter.Memory;
        if (type == typeof(ReadOnlyMemory<byte>))
            return _ => Base64Utf8Converter.ReadOnlyMemory;
        if (type == typeof(Memory<byte>))
            return _ => Base64Utf8Converter.Memory;
        if (type == typeof(byte[]))
            return _ => Base64Utf8Converter.Array;
        return null;
    }
}
