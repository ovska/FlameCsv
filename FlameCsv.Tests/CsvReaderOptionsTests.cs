using System.Globalization;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests;

public class CsvReaderOptionsTests
{
    [Fact]
    public void Should_Prioritize_Parsers_Added_Last()
    {
        var c1 = new CultureInfo("fi");
        var c2 = new CultureInfo("se");

        var options = new CsvReaderOptions<char>
        {
            Parsers =
            {
                new IntegerTextParser(formatProvider: c1),
                new IntegerTextParser(formatProvider: c2),
            },
        };

        Assert.Equal(c2, ((IntegerTextParser)options.GetParser<int>()).FormatProvider);
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var options = CsvTextReaderOptions.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal("\r\n", options.Newline.ToArray());

        var boolParser = options.GetParser<bool>();
        Assert.True(boolParser.TryParse("true", out var bValue));
        Assert.True(bValue);

        var intParser = options.GetParser<ushort>();
        Assert.True(intParser.TryParse("1234", out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetParser<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday", out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(options.TryGetParser(typeof(Type)));

        Assert.Same(options, CsvTextReaderOptions.Default);
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var options = CsvUtf8ReaderOptions.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal("\r\n"u8.ToArray(), options.Newline.ToArray());

        var boolParser = options.GetParser<bool>();
        Assert.True(boolParser.TryParse("true"u8, out var bValue));
        Assert.True(bValue);

        var intParser = options.GetParser<ushort>();
        Assert.True(intParser.TryParse("1234"u8, out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetParser<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday"u8, out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(options.TryGetParser(typeof(Type)));

        Assert.Same(options, CsvUtf8ReaderOptions.Default);
    }

    [Fact]
    public void Should_Return_Skip_Callback()
    {
        Assert.Throws<ArgumentException>(() => CsvReaderOptions<char>.SkipIfStartsWith(default));

        var tokens = CsvDialect<char>.Default.Clone(whitespace: " ".AsMemory());
        var commentfn = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: false);
        Assert.True(commentfn("#test", in tokens));
        Assert.False(commentfn("t#est", in tokens));
        Assert.False(commentfn("", in tokens));
        Assert.False(commentfn(" ", in tokens));

        var commentOrEmpty = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: true);
        Assert.True(commentOrEmpty("#test", in tokens));
        Assert.False(commentOrEmpty("t#est", in tokens));
        Assert.True(commentOrEmpty("", in tokens));
        Assert.True(commentOrEmpty(" ", in tokens));
    }

    [Fact]
    public void Should_Throw_On_ReadOnly_Modified()
    {
        var options = new CsvReaderOptions<char>();
        Assert.True(options.MakeReadOnly());
        Assert.False(options.MakeReadOnly());

        Run(o => o.Delimiter = default);
        Run(o => o.Quote = default);
        Run(o => o.Newline = default);
        Run(o => o.Whitespace = default);
        Run(o => o.ShouldSkipRow = default);
        Run(o => o.HasHeader = default);
        Run(o => o.HeaderBinder = default);
        Run(o => o.ExceptionHandler = default);
        Run(o => o.AllowContentInExceptions = default);
        Run(o => o.Parsers[0] = new IntegerTextParser());
        Run(o => o.Parsers.Add(new IntegerTextParser()));
        Run(o => o.Parsers.Insert(0, new IntegerTextParser()));
        Run(o => o.Parsers.Remove(new IntegerTextParser()));
        Run(o => o.Parsers.RemoveAt(0));
        Run(o => o.Parsers.Clear());

        void Run(Action<CsvReaderOptions<char>> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
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

        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

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
