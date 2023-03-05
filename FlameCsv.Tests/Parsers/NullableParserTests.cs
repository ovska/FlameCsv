using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests.Parsers;

public static class NullableParserTests
{
    [Fact]
    public static void Should_Return_Null()
    {
        var parser = new NullableParser<char, int>(
            new IntegerTextParser(),
            "".AsMemory());

        Assert.Equal("", parser.NullToken.ToString());

        Assert.True(parser.TryParse("", out var value1));
        Assert.Null(value1);

        Assert.True(parser.TryParse("1", out var value2));
        Assert.Equal(1, value2);

        Assert.False(parser.TryParse(" ", out _));
    }

    [Fact]
    public static void Should_Create_Parser()
    {
        var factory = new NullableParserFactory<char>("null".AsMemory());

        Assert.Equal("null", factory.NullToken.ToString());

        Assert.True(factory.CanParse(typeof(int?)));
        Assert.False(factory.CanParse(typeof(int)));

        var emptyOptions = new CsvReaderOptions<char> { Parsers = { factory, new IntegerTextParser() } };
        var parser = (ICsvParser<char, int?>)factory.Create(typeof(int?), emptyOptions);
        Assert.True(parser.TryParse("null", out var value));
        Assert.Null(value);
    }

    [Fact]
    public static void Should_Use_Options_NullToken()
    {
        var factory = new NullableParserFactory<char>("null".AsMemory());
        Assert.Equal("null", factory.NullToken.ToString());

        var options = new CsvTextReaderOptions { Null = "xyz" };
        var parser = (ICsvParser<char, int?>)factory.Create(typeof(int?), options);
        Assert.True(parser.TryParse("xyz", out var value));
        Assert.Null(value);
    }
}
