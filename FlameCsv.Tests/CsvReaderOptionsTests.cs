using System.Globalization;
using System.Runtime.InteropServices;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests;

public class CsvReaderOptionsTests
{
    [Fact]
    public static void Should_Validate_Text_ParseParams()
    {
        Do(o => o.IntegerNumberStyles = (NumberStyles)int.MaxValue);
        Do(o => o.DecimalNumberStyles = (NumberStyles)int.MaxValue);
        Do(o => o.DateTimeStyles = (DateTimeStyles)int.MaxValue);
        Do(o => o.TimeSpanStyles = (TimeSpanStyles)int.MaxValue);

        static void Do(Action<CsvTextReaderOptions> action)
        {
            Assert.ThrowsAny<ArgumentException>(() => action(new CsvTextReaderOptions()));
        }
    }

    [Fact]
    public static void Should_Validate_Utf8_Formats()
    {
        Do(o => o.DateTimeFormat = '^');
        Do(o => o.TimeSpanFormat = '^');
        Do(o => o.IntegerFormat = '^');
        Do(o => o.DecimalFormat = '^');
        Do(o => o.GuidFormat = '^');

        // no ex
        _ = new CsvUtf8ReaderOptions
        {
            DateTimeFormat = '\0',
            TimeSpanFormat = '\0',
            IntegerFormat = '\0',
            DecimalFormat = '\0',
            GuidFormat = '\0',
        };

        static void Do(Action<CsvUtf8ReaderOptions> action)
        {
            Assert.ThrowsAny<FormatException>(() => action(new CsvUtf8ReaderOptions()));
        }
    }

    [Fact]
    public static void Should_Validate_NullToken()
    {
        var to = new CsvTextReaderOptions();
        Assert.Throws<ArgumentException>(() => to.NullTokens[typeof(int)] = default);
        Assert.Throws<ArgumentException>(() => to.NullTokens[typeof(int*)] = default);
        Assert.Throws<ArgumentException>(() => to.NullTokens[typeof(Span<>)] = default);
        Assert.Throws<ArgumentException>(() => to.NullTokens[typeof(Span<int>)] = default);

        var bo = new CsvUtf8ReaderOptions();
        Assert.Throws<ArgumentException>(() => bo.NullTokens[typeof(int)] = default);
        Assert.Throws<ArgumentException>(() => bo.NullTokens[typeof(int*)] = default);
        Assert.Throws<ArgumentException>(() => bo.NullTokens[typeof(Span<>)] = default);
        Assert.Throws<ArgumentException>(() => bo.NullTokens[typeof(Span<int>)] = default);
    }

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
    public void Should_Not_Use_Builtin_Parsers()
    {
        var options = new CsvTextReaderOptions { UseDefaultParsers = false };
        Assert.Null(options.TryGetParser<int>());
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
    public void Should_Reuse_Common_Strings()
    {
        var options = new CsvUtf8ReaderOptions { Newline = "\r\n" };
        Assert.True(MemoryMarshal.TryGetArray(((ICsvDialectOptions<byte>)options).Newline, out var segment1));
        Assert.True(MemoryMarshal.TryGetArray(CsvDialectStatic._crlf, out var segment2));

        Assert.Equal(segment1.Count, segment2.Count);
        Assert.Equal(segment1.Offset, segment2.Offset);
        Assert.Same(segment1.Array, segment1.Array);

        var s1 = options.Newline;
        var s2 = options.Newline;
        Assert.Same(s1, s2);

        options.Newline = "\n";
        s1 = options.Newline;
        s2 = options.Newline;
        Assert.Same(s1, s2);

        options.Newline = "\r";
        s1 = options.Newline;
        s2 = options.Newline;
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var options = CsvUtf8ReaderOptions.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal("\r\n"u8.ToArray(), ((ICsvDialectOptions<byte>)options).Newline.ToArray());
        Assert.Equal("\r\n", options.Newline);

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
        Run(o => o.Newline = "");
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

    [Fact]
    public void Should_Validate_Field_Count()
    {
        const string data = "1,1,1\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvTextReaderOptions { ValidateFieldCount = true, HasHeader = false };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidDataException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Should_Validate_Field_Count_With_Header()
    {
        const string data = "A,B,C\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvTextReaderOptions { ValidateFieldCount = true, HasHeader = true };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidDataException>(() => enumerator.MoveNext());
    }
}
