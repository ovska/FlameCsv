using System.ComponentModel;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
static partial class DefaultConverters
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, DateTime> CreateDateTime(CsvTextOptions options) => new DateTimeTextConverter(options);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, DateTime> CreateDateTime(CsvUtf8Options options) => new DateTimeUtf8Converter(options.DateTimeFormat);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, DateTimeOffset> CreateDateTimeOffset(CsvTextOptions options) => new DateTimeOffsetTextConverter(options);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, DateTimeOffset> CreateDateTimeOffset(CsvUtf8Options options) => new DateTimeOffsetUtf8Converter(options.DateTimeFormat);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, Guid> CreateGuid(CsvTextOptions options) => new GuidTextConverter(options);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, Guid> CreateGuid(CsvUtf8Options options) => new GuidUtf8Converter(options.GuidFormat);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TimeSpan> CreateTimeSpan(CsvTextOptions options) => new TimeSpanTextConverter(options);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TimeSpan> CreateTimeSpan(CsvUtf8Options options) => new TimeSpanUtf8Converter(options.TimeSpanFormat);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, string> CreateString(CsvTextOptions options)
    {
        return options.StringPool is { } pool
            ? pool == StringPool.Shared ? PoolingStringTextConverter.SharedInstance : new PoolingStringTextConverter(pool)
            : StringTextConverter.Instance;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, string> CreateString(CsvUtf8Options options)
    {
        return options.StringPool is { } pool
            ? pool == StringPool.Shared ? PoolingStringUtf8Converter.SharedInstance : new PoolingStringUtf8Converter(pool)
            : StringUtf8Converter.Instance;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, bool> CreateBoolean(CsvTextOptions options)
    {
        return options._booleanValues is { Count: > 0 } values ? new CustomBooleanTextConverter(values) : BooleanTextConverter.Instance;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, bool> CreateBoolean(CsvUtf8Options options)
    {
        return options._booleanValues is { Count: > 0 } values ? new CustomBooleanUtf8Converter(values) : new BooleanUtf8Converter(options.BooleanFormat);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<char, TEnum> Create<TEnum>(CsvTextOptions options) where TEnum : struct, Enum => new EnumTextConverter<TEnum>(options);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvConverter<byte, TEnum> Create<TEnum>(CsvUtf8Options options) where TEnum : struct, Enum => new EnumUtf8Converter<TEnum>(options);
}
