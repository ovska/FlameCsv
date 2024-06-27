using System.Globalization;
using System.Runtime.InteropServices;
using FlameCsv.Converters;

namespace FlameCsv.Tests;

public class CsvOptionsTests
{
    [Fact]
    public static void Should_Validate_Text_ParseParams()
    {
        Do(o => o.IntegerNumberStyles = (NumberStyles)int.MaxValue);
        Do(o => o.DecimalNumberStyles = (NumberStyles)int.MaxValue);
        Do(o => o.DateTimeStyles = (DateTimeStyles)int.MaxValue);
        Do(o => o.TimeSpanStyles = (TimeSpanStyles)int.MaxValue);

        static void Do(Action<CsvTextOptions> action)
        {
            Assert.ThrowsAny<ArgumentException>(() => action(new CsvTextOptions()));
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
        _ = new CsvUtf8Options
        {
            DateTimeFormat = default,
            TimeSpanFormat = default,
            IntegerFormat = default,
            DecimalFormat = default,
            GuidFormat = default,
        };

        static void Do(Action<CsvUtf8Options> action)
        {
            Assert.ThrowsAny<FormatException>(() => action(new CsvUtf8Options()));
        }
    }

    [Fact]
    public static void Should_Validate_NullToken()
    {
        var to = new CsvTextOptions();
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[typeof(int)] = default);
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[typeof(int*)] = default);
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[typeof(Span<>)] = default);
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[typeof(Span<int>)] = default);

        var bo = new CsvUtf8Options();
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[typeof(int)] = default);
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[typeof(int*)] = default);
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[typeof(Span<>)] = default);
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[typeof(Span<int>)] = default);
    }

    [Fact]
    public void Should_Not_Use_Builtin_Parsers()
    {
        var options = new CsvTextOptions { UseDefaultConverters = false };
        Assert.Null(options.TryGetConverter<int>());
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var options = CsvTextOptions.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal([], options._newline.Span);
        Assert.Equal("", options.Newline);

        var boolParser = options.GetConverter<bool>();
        Assert.True(boolParser.TryParse("true", out var bValue));
        Assert.True(bValue);

        var intParser = options.GetConverter<ushort>();
        Assert.True(intParser.TryParse("1234", out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetConverter<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday", out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(options.TryGetConverter(typeof(Type)));

        Assert.Same(options, CsvTextOptions.Default);
    }

    [Fact]
    public void Should_Reuse_Common_Strings()
    {
        var options = new CsvUtf8Options { Newline = "\r\n" };
        Assert.True(MemoryMarshal.TryGetArray(((ICsvDialectOptions<byte>)options).Newline, out var segment1));
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)Utf8String.CRLF, out var segment2));

        Assert.Equal(segment1.Count, segment2.Count);
        Assert.Equal(segment1.Offset, segment2.Offset);
        Assert.Same(segment1.Array, segment1.Array);

        var s1 = (string)options.Newline;
        var s2 = (string)options.Newline;
        Assert.Same(s1, s2);

        options.Newline = "\n";
        s1 = (string)options.Newline;
        s2 = (string)options.Newline;
        Assert.Same(s1, s2);

        options.Newline = "\r";
        s1 = (string)options.Newline;
        s2 = (string)options.Newline;
        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var options = CsvUtf8Options.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal([], ((ICsvDialectOptions<byte>)options).Newline.ToArray());
        Assert.Equal("", options.Newline);

        var boolParser = options.GetConverter<bool>();
        Assert.True(boolParser.TryParse("true"u8, out var bValue));
        Assert.True(bValue);

        var intParser = options.GetConverter<ushort>();
        Assert.True(intParser.TryParse("1234"u8, out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetConverter<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday"u8, out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.Null(options.TryGetConverter(typeof(Type)));

        Assert.Same(options, CsvUtf8Options.Default);
    }

    [Fact]
    public void Should_Throw_On_ReadOnly_Modified()
    {
        var options = new CsvTextOptions();
        Assert.True(options.MakeReadOnly());
        Assert.False(options.MakeReadOnly());

        Run(o => o.Delimiter = default);
        Run(o => o.Quote = default);
        Run(o => o.Newline = "");
        Run(o => o.ShouldSkipRow = default);
        Run(o => o.HasHeader = default);
        Run(o => o.Comparer = StringComparer.Ordinal);
        Run(o => o.ExceptionHandler = default);
        Run(o => o.AllowContentInExceptions = default);
        Run(o => o.Converters[0] = new BooleanTextConverter());
        Run(o => o.Converters.Add(new BooleanTextConverter()));
        Run(o => o.Converters.Insert(0, new BooleanTextConverter()));
        Run(o => o.Converters.Remove(new BooleanTextConverter()));
        Run(o => o.Converters.RemoveAt(0));
        Run(o => o.Converters.Clear());

        void Run(Action<CsvTextOptions> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
    }

    [Fact]
    public void Should_Validate_Field_Count()
    {
        const string data = "1,1,1\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvTextOptions { ValidateFieldCount = true, HasHeader = false };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidDataException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Should_Validate_Field_Count_With_Header()
    {
        const string data = "A,B,C\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvTextOptions { ValidateFieldCount = true, HasHeader = true };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidDataException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Should_Skip_CsvRecord_Rows()
    {
        const string data =
            "sep=,\r\n" +
            "A,B,C\r\n" +
            "1,2,3\r\n" +
            "#4,5,6\r\n" +
            "7,8,9\r\n";

        var options = new CsvTextOptions
        {
            ShouldSkipRow = (in CsvRecordSkipArgs<char> args) => args.Line == 1 || args.Record.Span[0] == '#',
        };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.RawRecord.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("7,8,9", enumerator.Current.RawRecord.ToString());
    }

    [Fact]
    public void Should_Skip_Value_Rows()
    {
        const string data =
            "sep=,\r\n" +
            "A,B,C\r\n" +
            "1,2,3\r\n" +
            "#4,5,6\r\n" +
            "7,8,9\r\n";

        var options = new CsvTextOptions
        {
            ShouldSkipRow = (in CsvRecordSkipArgs<char> args) => args.Line == 1 || args.Record.Span[0] == '#',
        };

        var values = CsvReader.Read<Skippable>(data, options).ToList();

        Assert.Equal(2, values.Count);
        Assert.Equal(1, values[0].A);
        Assert.Equal(2, values[0].B);
        Assert.Equal(3, values[0].C);
        Assert.Equal(7, values[1].A);
        Assert.Equal(8, values[1].B);
        Assert.Equal(9, values[1].C);
    }

    private sealed class Skippable
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }
}
