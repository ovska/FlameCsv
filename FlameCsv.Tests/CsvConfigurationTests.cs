using System.Globalization;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests;

public class CsvConfigurationTests
{
    [Fact]
    public void Should_Support_Only_Byte_And_Char_Defaults()
    {
        Assert.Throws<NotSupportedException>(() => CsvConfiguration<int>.DefaultBuilder);
        Assert.Throws<NotSupportedException>(() => CsvConfiguration<int>.Default);
        Assert.Null(Record.Exception(() => CsvConfiguration<byte>.Default));
    }

    [Fact]
    public void Should_Validate_Parser_Count_When_Built()
    {
        Assert.Throws<CsvConfigurationException>(() => new CsvConfigurationBuilder<char>().Build());
    }

    [Fact]
    public void Should_Prioritize_Parsers_Added_Last()
    {
        var config = new CsvConfigurationBuilder<char>()
            .AddParser(new IntegerTextParser(formatProvider: CultureInfo.CurrentCulture))
            .AddParser(new IntegerTextParser(formatProvider: CultureInfo.InvariantCulture))
            .Build();

        Assert.Equal(
            CultureInfo.InvariantCulture,
            ((IntegerTextParser)config.GetParser<int>()).FormatProvider);
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var config = CsvConfiguration.GetTextDefaults();

        Assert.Equal("\r\n", config.Options.NewLine.ToArray());

        var boolParser = config.GetParser<bool>();
        Assert.True(boolParser.TryParse("true", out var bValue));
        Assert.True(bValue);

        var intParser = config.GetParser<ushort>();
        Assert.True(intParser.TryParse("1234", out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = config.GetParser<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday", out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(config.TryGetParser(typeof(Type)));
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var config = CsvConfiguration.GetUtf8Defaults();

        Assert.Equal(U8("\r\n"), config.Options.NewLine.ToArray());

        var boolParser = config.GetParser<bool>();
        Assert.True(boolParser.TryParse(U8("true"), out var bValue));
        Assert.True(bValue);

        var intParser = config.GetParser<ushort>();
        Assert.True(intParser.TryParse(U8("1234"), out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = config.GetParser<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse(U8("Monday"), out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(config.TryGetParser(typeof(Type)));

        static byte[] U8(string input) => Encoding.UTF8.GetBytes(input);
    }

    [Fact]
    public void Should_Return_Skip_Callback()
    {
        Assert.Throws<ArgumentException>(() => CsvConfiguration<char>.SkipIfStartsWith(default));

        var options = CsvParserOptions<char>.Windows with { Whitespace = " ".AsMemory() };
        var commentfn = CsvConfiguration<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: false);
        Assert.True(commentfn("#test", in options));
        Assert.False(commentfn("t#est", in options));
        Assert.False(commentfn("", in options));
        Assert.False(commentfn(" ", in options));

        var commentOrEmpty = CsvConfiguration<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: true);
        Assert.True(commentOrEmpty("#test", in options));
        Assert.False(commentOrEmpty("t#est", in options));
        Assert.True(commentOrEmpty("", in options));
        Assert.True(commentOrEmpty(" ", in options));
    }
}
