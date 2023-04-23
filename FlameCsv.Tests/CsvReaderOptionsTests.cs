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

        var options = new CsvTextReaderOptions
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

        var tokens = CsvDialect<char>.Default;
        var commentfn = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: false);
        Assert.True(commentfn("#test".AsMemory(), in tokens));
        Assert.False(commentfn("t#est".AsMemory(), in tokens));
        Assert.False(commentfn("".AsMemory(), in tokens));
        Assert.False(commentfn(" ".AsMemory(), in tokens));

        var commentOrEmpty = CsvReaderOptions<char>.SkipIfStartsWith("#", skipEmptyOrWhitespace: true);
        Assert.True(commentOrEmpty("#test".AsMemory(), in tokens));
        Assert.False(commentOrEmpty("t#est".AsMemory(), in tokens));
        Assert.True(commentOrEmpty("".AsMemory(), in tokens));
        Assert.False(commentOrEmpty(" ".AsMemory(), in tokens));
    }

    [Fact]
    public void Should_Throw_On_ReadOnly_Modified()
    {
        var options = new CsvTextReaderOptions();
        Assert.True(options.MakeReadOnly());
        Assert.False(options.MakeReadOnly());

        Run(o => o.Delimiter = default);
        Run(o => o.Quote = default);
        Run(o => o.Newline = default);
        Run(o => o.ShouldSkipRow = default);
        Run(o => o.HasHeader = default);
        Run(o => o.Comparison = default);
        Run(o => o.ExceptionHandler = default);
        Run(o => o.AllowContentInExceptions = default);
        Run(o => o.Parsers[0] = new IntegerTextParser());
        Run(o => o.Parsers.Add(new IntegerTextParser()));
        Run(o => o.Parsers.Insert(0, new IntegerTextParser()));
        Run(o => o.Parsers.Remove(new IntegerTextParser()));
        Run(o => o.Parsers.RemoveAt(0));
        Run(o => o.Parsers.Clear());

        void Run(Action<CsvTextReaderOptions> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
    }
}
