using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv;

public partial class CsvConfiguration<T>
{
    /// <summary>
    /// Returns the default text or UTF8 configuration.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    /// <seealso cref="CsvConfiguration.GetTextDefaults"/>
    /// <seealso cref="CsvConfiguration.GetUtf8Defaults"/>
    public static CsvConfigurationBuilder<T> DefaultBuilder
    {
        get
        {
            if (typeof(T) == typeof(char))
                return (CsvConfigurationBuilder<T>)(object)CsvConfiguration.GetTextDefaultsBuilder();

            if (typeof(T) == typeof(byte))
                return (CsvConfigurationBuilder<T>)(object)CsvConfiguration.GetUtf8DefaultsBuilder();

            throw new NotSupportedException($"Default configuration for {typeof(T)} is not supported.");
        }
    }

    /// <summary>
    /// Returns a builder for the default text or UTF8 configuration.
    /// </summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    /// <seealso cref="CsvConfiguration.GetTextDefaultsBuilder"/>
    /// <seealso cref="CsvConfiguration.GetUtf8DefaultsBuilder"/>
    public static CsvConfiguration<T> Default => _default ??= DefaultBuilder.Build();

    private static CsvConfiguration<T>? _default;
}

public static class CsvConfiguration
{
    /// <summary>
    /// Returns a builder with default parsers optionally configured by the passed configuration object.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CsvParserOptions{T}.Windows"/> (RFC 4180 uses CRLF) with the following
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
    public static CsvConfigurationBuilder<char> GetTextDefaultsBuilder(
        CsvTextParserConfiguration? config = null)
    {
        config ??= new();

        var builder = new CsvConfigurationBuilder<char>()
            .AddParser(
                config.StringPool is { } stringPool
                    ? new PoolingStringTextParser(stringPool, config.ReadEmptyStringsAsNull)
                    : new StringTextParser(config.ReadEmptyStringsAsNull))
            .AddParser(new IntegerTextParser(config.IntegerNumberStyles, config.FormatProvider))
            .AddParser(new BooleanTextParser(config.BooleanValues))
            .AddParser(new DateTimeTextParser(config.DateTimeFormat, config.FormatProvider, config.DateTimeStyles))
            .AddParser(new DecimalTextParser(config.DecimalNumberStyles, config.FormatProvider))
            .AddParser(new EnumTextParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase))
            .AddParser(new NullableParserFactory<char>(config.Null.AsMemory()))
            .AddParser(new GuidTextParser(config.GuidFormat))
            .AddParser(new TimeSpanTextParser(config.TimeSpanFormat, config.FormatProvider))
            .AddParser(new Base64TextParser())
            .AddParser(new DateOnlyTextParser(config.DateOnlyFormat, config.DateTimeStyles, config.FormatProvider))
            .AddParser(new TimeOnlyTextParser(config.TimeOnlyFormat, config.DateTimeStyles, config.FormatProvider));

        builder._parsers.Reverse(); // HACK: add most common types last so they are checked first
        return builder;
    }

    /// <inheritdoc cref="GetTextDefaultsBuilder"/>
    /// <summary>
    /// Returns a built configuration with default parsers configured by the passed configuration object.
    /// </summary>
    public static CsvConfiguration<char> GetTextDefaults(
        CsvTextParserConfiguration? config = null)
    {
        return GetTextDefaultsBuilder(config).Build();
    }

    /// <summary>
    /// Returns a builder with default parsers optionally configured by the passed configuration object.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CsvParserOptions{T}.Windows"/> (RFC 4180 uses CRLF) with the following
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
    public static CsvConfigurationBuilder<byte> GetUtf8DefaultsBuilder(
        CsvUtf8ParserConfiguration? config = null)
    {
        config ??= new();

        var builder = new CsvConfigurationBuilder<byte>()
            .AddParser(new StringUtf8Parser())
            .AddParser(new IntegerUtf8Parser(config.IntegerFormat))
            .AddParser(new BooleanUtf8Parser(config.BooleanValues))
            .AddParser(new DateTimeUtf8Parser(config.DateTimeFormat))
            .AddParser(new DecimalUtf8Parser(config.DecimalFormat))
            .AddParser(new EnumUtf8ParserFactory(config.AllowUndefinedEnumValues, config.IgnoreEnumCase))
            .AddParser(new NullableParserFactory<byte>(config.Null))
            .AddParser(new GuidUtf8Parser(config.GuidFormat))
            .AddParser(new TimeSpanUtf8Parser(config.TimeSpanFormat))
            .AddParser(new Base64Utf8Parser());

        builder._parsers.Reverse(); // HACK: add most common types last so they are checked first
        return builder;
    }

    /// <inheritdoc cref="GetTextDefaultsBuilder"/>
    /// <summary>
    /// Returns a built configuration with default parsers configured by the passed configuration object.
    /// </summary>
    public static CsvConfiguration<byte> GetUtf8Defaults(
        CsvUtf8ParserConfiguration? config = null)
    {
        return GetUtf8DefaultsBuilder(config).Build();
    }
}
