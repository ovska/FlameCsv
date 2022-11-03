using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv;

public partial class CsvReaderOptions<T>
{
    /// <summary>
    /// Returns a builder for the default text or UTF8 configuration.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    /// <seealso cref="CsvOptions.GetTextReaderDefault"/>
    /// <seealso cref="CsvOptions.GetUtf8ReaderDefault"/>
    public static CsvReaderOptions<T> Default
    {
        get
        {
            if (typeof(T) == typeof(char))
                return (CsvReaderOptions<T>)(object)CsvOptions.GetTextReaderDefault();

            if (typeof(T) == typeof(byte))
                return (CsvReaderOptions<T>)(object)CsvOptions.GetUtf8ReaderDefault();

            throw new NotSupportedException($"Default configuration for {typeof(T)} is not supported.");
        }
    }
}

public static class CsvOptions
{
    /// <summary>
    /// Returns a builder with default parsers optionally configured by the passed configuration object.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CsvTokens{T}.Windows"/> (RFC 4180 uses CRLF) with the following
    /// parsers/factories using values from the configuration:
    /// <list type="bullet">
    /// <item><see cref="StringTextParser"/> or <see cref="PoolingStringTextParser"/></item>
    /// <item><see cref="IntegerTextParser"/></item>
    /// <item><see cref="BooleanTextParser"/></item>
    /// <item><see cref="DateTimeTextParser"/></item>
    /// <item><see cref="EnumTextParserFactory"/></item>
    /// <item><see cref="NullableParserFactory{T}"/></item>
    /// <item><see cref="DecimalTextParser"/></item>
    /// <item><see cref="GuidTextParser"/></item>
    /// <item><see cref="TimeSpanTextParser"/></item>
    /// <item><see cref="Base64TextParser"/></item>
    /// <item><see cref="DateOnlyTextParser"/></item>
    /// <item><see cref="TimeOnlyTextParser"/></item>
    /// </list>
    /// </remarks>
    /// <param name="config">Optional custom configuration</param>
    public static CsvReaderOptions<char> GetTextReaderDefault(
        CsvTextParsersConfig? config = null)
    {
        config ??= CsvTextParsersConfig.Default;

        // TODO: if options are default, return a readonly default instance to avoid allocations
        var options = new CsvReaderOptions<char>()
            .AddParsers(
                config.StringPool is { } stringPool
                    ? new PoolingStringTextParser(stringPool, config.ReadEmptyStringsAsNull)
                    : new StringTextParser(config.ReadEmptyStringsAsNull),
                new IntegerTextParser(config.IntegerNumberStyles, config.FormatProvider),
                new BooleanTextParser(config.BooleanValues),
                new DateTimeTextParser(config.DateTimeFormat, config.FormatProvider, config.DateTimeStyles),
                new DecimalTextParser(config.DecimalNumberStyles, config.FormatProvider),
                new EnumTextParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase),
                new NullableParserFactory<char>(config.Null.AsMemory()),
                new GuidTextParser(config.GuidFormat),
                new TimeSpanTextParser(config.TimeSpanFormat, config.FormatProvider, config.TimeSpanStyles),
                new Base64TextParser(),
                new DateOnlyTextParser(config.DateOnlyFormat, config.DateTimeStyles, config.FormatProvider),
                new TimeOnlyTextParser(config.TimeOnlyFormat, config.DateTimeStyles, config.FormatProvider));

        // no need to lock as this instance isn't yet exposed outside this method
        options._parsers.Reverse(); // HACK: add most common types last so they are checked first
        return options;
    }

    /// <summary>
    /// Returns a builder with default parsers optionally configured by the passed configuration object.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CsvTokens{T}.Windows"/> (RFC 4180 uses CRLF) with the following
    /// parsers/factories using values from the configuration:
    /// <list type="bullet">
    /// <item><see cref="StringUtf8Parser"/></item>
    /// <item><see cref="IntegerUtf8Parser"/></item>
    /// <item><see cref="BooleanUtf8Parser"/></item>
    /// <item><see cref="DateTimeUtf8Parser"/></item>
    /// <item><see cref="DecimalUtf8Parser"/></item>
    /// <item><see cref="EnumUtf8ParserFactory"/></item>
    /// <item><see cref="NullableParserFactory{T}"/></item>
    /// <item><see cref="GuidUtf8Parser"/></item>
    /// <item><see cref="TimeSpanUtf8Parser"/></item>
    /// <item><see cref="Base64Utf8Parser"/></item>
    /// </list>
    /// </remarks>
    /// <param name="config">Optional custom configuration</param>
    public static CsvReaderOptions<byte> GetUtf8ReaderDefault(
        CsvUtf8ParsersConfig? config = null)
    {
        config ??= CsvUtf8ParsersConfig.Default;

        // TODO: if options are default, return a readonly default instance to avoid allocations
        var options = new CsvReaderOptions<byte>()
            .AddParsers(
                new StringUtf8Parser(),
                new IntegerUtf8Parser(config.IntegerFormat),
                new BooleanUtf8Parser(config.BooleanValues),
                new DateTimeUtf8Parser(config.DateTimeFormat),
                new DecimalUtf8Parser(config.DecimalFormat),
                new EnumUtf8ParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase),
                new NullableParserFactory<byte>(config.Null),
                new GuidUtf8Parser(config.GuidFormat),
                new TimeSpanUtf8Parser(config.TimeSpanFormat),
                new Base64Utf8Parser());

        // no need to lock as this instance isn't yet exposed outside this method
        options._parsers.Reverse(); // HACK: add most common types last so they are checked first
        return options;
    }
}
