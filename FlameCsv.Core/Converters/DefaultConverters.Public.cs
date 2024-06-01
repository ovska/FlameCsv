using System.ComponentModel;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
static partial class DefaultConverters
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TValue> GetOrCreate<TValue>(
        CsvTextOptions options,
        Func<CsvTextOptions, CsvConverter<char, TValue>> converterFactory)
    {
        return options.GetOrCreate(converterFactory);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TValue> GetOrCreate<TValue>(
        CsvUtf8Options options,
        Func<CsvUtf8Options, CsvConverter<byte, TValue>> converterFactory)
    {
        return options.GetOrCreate(converterFactory);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, DateTime> CreateDateTime(CsvTextOptions options) => options.GetOrCreate(static o => new DateTimeTextConverter(o));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, DateTime> CreateDateTime(CsvUtf8Options options) => options.GetOrCreate(static o => new DateTimeUtf8Converter(o.DateTimeFormat));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, DateTimeOffset> CreateDateTimeOffset(CsvTextOptions options) => options.GetOrCreate(static o => new DateTimeOffsetTextConverter(o));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, DateTimeOffset> CreateDateTimeOffset(CsvUtf8Options options) => options.GetOrCreate(static o => new DateTimeOffsetUtf8Converter(o.DateTimeFormat));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, Guid> CreateGuid(CsvTextOptions options) => options.GetOrCreate(static o => new GuidTextConverter(o));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, Guid> CreateGuid(CsvUtf8Options options) => options.GetOrCreate(static o => new GuidUtf8Converter(o.GuidFormat));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TimeSpan> CreateTimeSpan(CsvTextOptions options) => options.GetOrCreate(static o => new TimeSpanTextConverter(o));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TimeSpan> CreateTimeSpan(CsvUtf8Options options) => options.GetOrCreate(static o => new TimeSpanUtf8Converter(o.TimeSpanFormat));

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, string> CreateString(CsvTextOptions options)
    {
        return options.GetOrCreate<string>(static o => o.StringPool is not null
            ? new PoolingStringTextConverter(o)
            : new StringTextConverter(o));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, string> CreateString(CsvUtf8Options options)
    {
        return options.GetOrCreate<string>(static o => o.StringPool is not null
            ? new PoolingStringUtf8Converter(o)
            : new StringUtf8Converter(o));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, bool> CreateBoolean(CsvTextOptions options)
    {
        return options.GetOrCreate<bool>(static o => o._booleanValues is { Count: > 0 } values ? new CustomBooleanTextConverter(values) : BooleanTextConverter.Instance);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, bool> CreateBoolean(CsvUtf8Options options)
    {
        return options.GetOrCreate<bool>(static o => o._booleanValues is { Count: > 0 } values ? new CustomBooleanUtf8Converter(values) : new BooleanUtf8Converter(o.BooleanFormat));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TEnum> Create<TEnum>(CsvTextOptions options) where TEnum : struct, Enum
    {
        return options.GetOrCreate(static o => new EnumTextConverter<TEnum>(o));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TEnum> Create<TEnum>(CsvUtf8Options options) where TEnum : struct, Enum
    {
        return options.GetOrCreate(static o => new EnumUtf8Converter<TEnum>(o));
    }
}
