using FlameCsv.Exceptions;

namespace FlameCsv.Tests;

public static class DialectTests
{
    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal(',', CsvOptions<char>.Default.Dialect.Delimiter);
        Assert.Equal('"', CsvOptions<char>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<char>.Default.Dialect.Escape);
        Assert.Empty(CsvOptions<char>.Default.Dialect.Newline.ToArray());
        Assert.Empty(CsvOptions<char>.Default.Dialect.Whitespace.ToArray());

        Assert.Equal((byte)',', CsvOptions<byte>.Default.Dialect.Delimiter);
        Assert.Equal((byte)'"', CsvOptions<byte>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<byte>.Default.Dialect.Escape);
        Assert.Empty(CsvOptions<byte>.Default.Dialect.Newline.ToArray());
        Assert.Empty(CsvOptions<byte>.Default.Dialect.Whitespace.ToArray());
    }

    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<InvalidOperationException>(() => default(CsvDialect<char>).Validate());

        AssertInvalid(o => o with { Quote = ',' });
        AssertInvalid(o => o with { Newline = ",".AsMemory() });
        AssertInvalid(o => o with { Newline = "\"".AsMemory() });
        AssertInvalid(o => o with { Delimiter = '\n' });
        AssertInvalid(o => o with { Escape = ',' });
        AssertInvalid(o => o with { Whitespace = ",".AsMemory() });

        static void AssertInvalid(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() =>
            {
                action(new CsvOptions<char>().Dialect).Validate();
            });
        }
    }
}
