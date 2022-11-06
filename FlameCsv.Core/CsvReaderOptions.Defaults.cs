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
        var options = new CsvReaderOptions<char>();

        // parsers are readonly, for default options we can save on allocations by using the unconfigured ones
        if (config is null)
        {
            options._parsers.AddRange(_defaultTextParsers);
        }
        else
        {
            options._parsers.EnsureCapacity(_defaultTextParsers.Length);
            options._parsers.Add(
                config.StringPool is { } stringPool
                    ? new PoolingStringTextParser(stringPool, config.ReadEmptyStringsAsNull)
                    : new StringTextParser(config.ReadEmptyStringsAsNull));
            options._parsers.Add(new IntegerTextParser(config.IntegerNumberStyles, config.FormatProvider));
            options._parsers.Add(new BooleanTextParser(config.BooleanValues));
            options._parsers.Add(
                new DateTimeTextParser(config.DateTimeFormat, config.FormatProvider, config.DateTimeStyles));
            options._parsers.Add(new DecimalTextParser(config.DecimalNumberStyles, config.FormatProvider));
            options._parsers.Add(new EnumTextParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase));
            options._parsers.Add(new NullableParserFactory<char>(config.Null.AsMemory()));
            options._parsers.Add(new GuidTextParser(config.GuidFormat));
            options._parsers.Add(
                new TimeSpanTextParser(config.TimeSpanFormat, config.FormatProvider, config.TimeSpanStyles));
            options._parsers.Add(new Base64TextParser());
            options._parsers.Add(
                new DateOnlyTextParser(config.DateOnlyFormat, config.DateTimeStyles, config.FormatProvider));
            options._parsers.Add(
                new TimeOnlyTextParser(config.TimeOnlyFormat, config.DateTimeStyles, config.FormatProvider));
        }

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
        var options = new CsvReaderOptions<byte>();

        if (config is null)
        {
            options._parsers.AddRange(_defaultByteParsers);
        }
        else
        {
            options._parsers.EnsureCapacity(_defaultByteParsers.Length);
            options._parsers.Add(StringUtf8Parser.Instance);
            options._parsers.Add(new IntegerUtf8Parser(config.IntegerFormat));
            options._parsers.Add(new BooleanUtf8Parser(config.BooleanValues));
            options._parsers.Add(new DateTimeUtf8Parser(config.DateTimeFormat));
            options._parsers.Add(new DecimalUtf8Parser(config.DecimalFormat));
            options._parsers.Add(new EnumUtf8ParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase));
            options._parsers.Add(new NullableParserFactory<byte>(config.Null));
            options._parsers.Add(new GuidUtf8Parser(config.GuidFormat));
            options._parsers.Add(new TimeSpanUtf8Parser(config.TimeSpanFormat));
            options._parsers.Add(Base64Utf8Parser.Instance);
        }

        return options;
    }

    private static readonly ICsvParser<char>[] _defaultTextParsers =
    {
        new StringTextParser(CsvTextParsersConfig.Default.ReadEmptyStringsAsNull),
        new IntegerTextParser(
            CsvTextParsersConfig.Default.IntegerNumberStyles,
            CsvTextParsersConfig.Default.FormatProvider),
        new BooleanTextParser(CsvTextParsersConfig.Default.BooleanValues),
        new DateTimeTextParser(
            CsvTextParsersConfig.Default.DateTimeFormat,
            CsvTextParsersConfig.Default.FormatProvider,
            CsvTextParsersConfig.Default.DateTimeStyles),
        new DecimalTextParser(
            CsvTextParsersConfig.Default.DecimalNumberStyles,
            CsvTextParsersConfig.Default.FormatProvider),
        new EnumTextParserFactory(
            CsvTextParsersConfig.Default.AllowUndefinedEnumValues,
            CsvTextParsersConfig.Default.IgnoreEnumCase),
        new NullableParserFactory<char>(CsvTextParsersConfig.Default.Null.AsMemory()),
        new GuidTextParser(CsvTextParsersConfig.Default.GuidFormat),
        new TimeSpanTextParser(
            CsvTextParsersConfig.Default.TimeSpanFormat,
            CsvTextParsersConfig.Default.FormatProvider,
            CsvTextParsersConfig.Default.TimeSpanStyles),
        new Base64TextParser(),
        new DateOnlyTextParser(
            CsvTextParsersConfig.Default.DateOnlyFormat,
            CsvTextParsersConfig.Default.DateTimeStyles,
            CsvTextParsersConfig.Default.FormatProvider),
        new TimeOnlyTextParser(
            CsvTextParsersConfig.Default.TimeOnlyFormat,
            CsvTextParsersConfig.Default.DateTimeStyles,
            CsvTextParsersConfig.Default.FormatProvider),
    };

    private static readonly ICsvParser<byte>[] _defaultByteParsers =
    {
        StringUtf8Parser.Instance,
        new IntegerUtf8Parser(CsvUtf8ParsersConfig.Default.IntegerFormat),
        new BooleanUtf8Parser(CsvUtf8ParsersConfig.Default.BooleanValues),
        new DateTimeUtf8Parser(CsvUtf8ParsersConfig.Default.DateTimeFormat),
        new DecimalUtf8Parser(CsvUtf8ParsersConfig.Default.DecimalFormat),
        new EnumUtf8ParserFactory(
            CsvUtf8ParsersConfig.Default.AllowUndefinedEnumValues,
            CsvUtf8ParsersConfig.Default.IgnoreEnumCase),
        new NullableParserFactory<byte>(CsvUtf8ParsersConfig.Default.Null),
        new GuidUtf8Parser(CsvUtf8ParsersConfig.Default.GuidFormat),
        new TimeSpanUtf8Parser(CsvUtf8ParsersConfig.Default.TimeSpanFormat),
        Base64Utf8Parser.Instance,
    };
}
