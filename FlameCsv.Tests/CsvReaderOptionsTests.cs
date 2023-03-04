using System.Globalization;
using System.Text;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests;

public class CsvReaderOptionsTests
{
    [Fact]
    public void Should_Support_Only_Byte_And_Char_Defaults()
    {
        Assert.Throws<NotSupportedException>(() => CsvReaderOptions<int>.Default);
        Assert.Null(Record.Exception(() => CsvReaderOptions<byte>.Default));
    }

    [Fact]
    public void Should_Prioritize_Parsers_Added_Last()
    {
        var config = new CsvReaderOptions<char>
        {
            Parsers =
            {
                new IntegerTextParser(formatProvider: CultureInfo.CurrentCulture),
                new IntegerTextParser(formatProvider: CultureInfo.InvariantCulture),
            },
        };

        Assert.Equal(
            CultureInfo.InvariantCulture,
            ((IntegerTextParser)config.GetParser<int>()).FormatProvider);
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var config = CsvReaderOptions<char>.Default;

        Assert.Equal("\r\n", config.Tokens.NewLine.ToArray());

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
        var config = CsvReaderOptions<byte>.Default;

        Assert.Equal(U8("\r\n"), config.Tokens.NewLine.ToArray());

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
        Assert.Throws<ArgumentException>(() => CsvReaderOptions<char>.SkipIfStartsWith(default));

        var options = CsvTokens<char>.Windows with { Whitespace = " ".AsMemory() };
        var commentfn = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: false);
        Assert.True(commentfn("#test", in options));
        Assert.False(commentfn("t#est", in options));
        Assert.False(commentfn("", in options));
        Assert.False(commentfn(" ", in options));

        var commentOrEmpty = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: true);
        Assert.True(commentOrEmpty("#test", in options));
        Assert.False(commentOrEmpty("t#est", in options));
        Assert.True(commentOrEmpty("", in options));
        Assert.True(commentOrEmpty(" ", in options));
    }

    [Fact(Skip = "Thread safety of mutability is no longer guaranteed")]
    public void Should_Be_Threadsafe()
    {
        // this test isn't reliable to prove a negative, but should work as
        // a canary in the coal mine
        var options = new CsvReaderOptions<char>();

        var parser = Base64TextParser.Instance;
        Thread[] threads =
        {
            new(Repeat(o => o.Parsers.Add(parser))),
            new(
                Repeat(
                    o =>
                    {
                        foreach (var x in o.EnumerateParsers()) _ = x;
                    })),
        };

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        Assert.Equal(1000, options.Parsers.Count);

        ThreadStart Repeat(Action<CsvReaderOptions<char>> action)
        {
            return () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    action(options);
                }
            };
        }
    }
}
