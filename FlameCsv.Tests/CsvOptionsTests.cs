using System.Globalization;
using FlameCsv.Converters;

namespace FlameCsv.Tests;

public class CsvOptionsTests
{
    private static ReadOnlySpan<T> Format<T, TValue>(CsvOptions<T> options, TValue value)
        where T : unmanaged, IBinaryInteger<T>
    {
        T[] buffer = new T[64];
        var converter = options.GetConverter<TValue>();
        Assert.True(converter.TryFormat(buffer, value, out int charsWritten));
        return buffer.AsSpan(0, charsWritten);
    }

    [Fact]
    public static void Should_Return_Null_Token()
    {
        var to = new CsvOptions<char>
        {
            NullTokens =
            {
                [typeof(long)] = "long",
                [typeof(short?)] = "short",
            },
            Null = "null",
        };

        Assert.Equal("long", to.GetNullToken(typeof(long)).ToString());
        Assert.Equal("long", to.GetNullToken(typeof(long?)).ToString());
        Assert.Equal("short", to.GetNullToken(typeof(short)).ToString());
        Assert.Equal("short", to.GetNullToken(typeof(short?)).ToString());
        Assert.Equal("null", to.GetNullToken(typeof(int)).ToString());
        Assert.Equal("null", to.GetNullToken(typeof(int?)).ToString());

        Assert.Equal("123", Format(to, 123L));
        Assert.Equal("long", Format(to, new long?()));
        Assert.Equal("123", Format(to, 123));
        Assert.Equal("null", Format(to, new int?()));

        var bo = new CsvOptions<byte>
        {
            NullTokens =
            {
                [typeof(long)] = "long",
                [typeof(short?)] = "short",
            },
            Null = "null",
        };

        Assert.Equal("long"u8, bo.GetNullToken(typeof(long)).Span);
        Assert.Equal("long"u8, bo.GetNullToken(typeof(long?)).Span);
        Assert.Equal("short"u8, bo.GetNullToken(typeof(short)).Span);
        Assert.Equal("short"u8, bo.GetNullToken(typeof(short?)).Span);
        Assert.Equal("null"u8, bo.GetNullToken(typeof(int)).Span);
        Assert.Equal("null"u8, bo.GetNullToken(typeof(int?)).Span);

        Assert.Equal("123"u8, Format(bo, 123L));
        Assert.Equal("long"u8, Format(bo, new long?()));
        Assert.Equal("123"u8, Format(bo, 123));
        Assert.Equal("null"u8, Format(bo, new int?()));
    }

    [Fact]
    public static void Should_Return_Format()
    {
        var to = new CsvOptions<char>
        {
            EnumFormat = "x",
            Formats = { [typeof(DayOfWeek)] = "d", [typeof(float)] = "0.0" },
        };

        Assert.Equal("1", Format(to, (DayOfWeek)1));
        Assert.Equal("00000012", Format(to, TypeCode.String));
        Assert.Equal("1.2", Format(to, 1.2345f));

        var bo = new CsvOptions<byte>
        {
            EnumFormat = "x",
            Formats = { [typeof(DayOfWeek)] = "d", [typeof(float)] = "0.0" },
        };

        Assert.Equal("1"u8, Format(bo, (DayOfWeek)1));
        Assert.Equal("00000012"u8, Format(bo, TypeCode.String));
        Assert.Equal("1.2"u8, Format(bo, 1.2345f));
    }

    [Fact]
    public static void Should_Return_FormatProvider()
    {
        var to = new CsvOptions<char>
        {
            FormatProvider = new CultureInfo("fi"),
            FormatProviders = { [typeof(double)] = CultureInfo.InvariantCulture },
        };

        Assert.Equal("fi", ((CultureInfo)to.GetFormatProvider(typeof(string))!).Name);
        Assert.Equal(CultureInfo.InvariantCulture, to.GetFormatProvider(typeof(double)));

        Assert.Equal("0,5", Format(to, 0.5f));
        Assert.Equal("0.5", Format(to, 0.5d));

        var bo = new CsvOptions<byte>
        {
            FormatProvider = new CultureInfo("fi"),
            FormatProviders = { [typeof(double)] = CultureInfo.InvariantCulture },
        };

        Assert.Equal("fi", ((CultureInfo)bo.GetFormatProvider(typeof(string))!).Name);
        Assert.Equal(CultureInfo.InvariantCulture, bo.GetFormatProvider(typeof(double)));

        Assert.Equal("0,5"u8, Format(bo, 0.5f));
        Assert.Equal("0.5"u8, Format(bo, 0.5d));
    }

    [Fact]
    public static void Should_Return_NumberStyles()
    {
        var to = new CsvOptions<char>
        {
            NumberStyles =
            {
                [typeof(int)] = NumberStyles.AllowTrailingWhite,
                [typeof(long)] = NumberStyles.AllowLeadingWhite,
            }
        };

        Assert.True(to.GetConverter<int>().TryParse("1  ", out _));
        Assert.False(to.GetConverter<long>().TryParse("1  ", out _));
        Assert.False(to.GetConverter<int>().TryParse("  1", out _));
        Assert.True(to.GetConverter<long>().TryParse("  1", out _));

        var bo = new CsvOptions<byte>
        {
            NumberStyles =
            {
                [typeof(int)] = NumberStyles.AllowTrailingWhite,
                [typeof(long)] = NumberStyles.AllowLeadingWhite,
            }
        };

        Assert.True(bo.GetConverter<int>().TryParse("1  "u8, out _));
        Assert.False(bo.GetConverter<long>().TryParse("1  "u8, out _));
        Assert.False(bo.GetConverter<int>().TryParse("  1"u8, out _));
        Assert.True(bo.GetConverter<long>().TryParse("  1"u8, out _));
    }

    [Theory, InlineData(typeof(int*)), InlineData(typeof(List<>)), InlineData(typeof(Span<int>)), InlineData(default(Type))]
    public static void Should_Validate_TypeDictionary_Argument(Type? type)
    {
        var to = new CsvOptions<char>();
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[type!] = "");

        var bo = new CsvOptions<byte>();
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[type!] = "");
    }

    [Fact]
    public void Should_Not_Use_Builtin_Parsers()
    {
        var options = new CsvOptions<char> { UseDefaultConverters = false };
        Assert.Null(options.TryGetConverter<int>());
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var options = CsvOptions<char>.Default;
        Assert.True(options.IsReadOnly);

        Assert.Null(options.Newline);
        var newline =  options.GetNewline();
        Assert.Equal(0, newline.Length);
        Assert.Equal(['\0', '\0'], [newline.First, newline.Second]);
        newline =  options.GetNewline(forWriting: true);
        Assert.Equal(2, newline.Length);
        Assert.Equal(['\r', '\n'], [newline.First, newline.Second]);

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

        Assert.Same(options, CsvOptions<char>.Default);
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var options = CsvOptions<byte>.Default;
        Assert.True(options.IsReadOnly);

        var newline =  options.GetNewline();
        Assert.Equal(0, newline.Length);
        Assert.Equal([0, 0], [newline.First, newline.Second]);
        newline =  options.GetNewline(forWriting: true);
        Assert.Equal(2, newline.Length);
        Assert.Equal([(byte)'\r', (byte)'\n'], [newline.First, newline.Second]);

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

        Assert.Same(options, CsvOptions<byte>.Default);
    }

    [Fact]
    public void Should_Throw_On_ReadOnly_Modified()
    {
        var options = new CsvOptions<char>();
        options.MakeReadOnly();

        Run(o => o.Delimiter = default);
        Run(o => o.Quote = default);
        Run(o => o.Escape = default);
        Run(o => o.Newline = "");
        Run(o => o.Whitespace = "");
        Run(o => o.ShouldSkipRow = default);
        Run(o => o.HasHeader = default);
        Run(o => o.Comparer = StringComparer.Ordinal);
        Run(o => o.ExceptionHandler = default);
        Run(o => o.Converters[0] = new BooleanTextConverter());
        Run(o => o.Converters.Add(new BooleanTextConverter()));
        Run(o => o.Converters.Insert(0, new BooleanTextConverter()));
        Run(o => o.Converters.Remove(new BooleanTextConverter()));
        Run(o => o.Converters.RemoveAt(0));
        Run(o => o.Converters.Clear());
        Run(o => o.StringPool = null);
        Run(o => o.IgnoreEnumCase = default);
        Run(o => o.AllowUndefinedEnumValues = default);
        Run(o => o.UseDefaultConverters = default);
        Run(o => o.EnumFormat = "");
        Run(o => o.FieldQuoting = default);
        Run(o => o.FormatProvider = default);
        Run(o => o.FormatProviders.Add(typeof(int), null));
        Run(o => o.Formats.Add(typeof(int), null));
        Run(o => o.NoLineBuffering = true);
        Run(o => o.Null = "null");
        Run(o => o.NumberStyles.Add(typeof(int), NumberStyles.None));
        Run(o => o.BooleanValues.Add(default));

        void Run(Action<CsvOptions<char>> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
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

        var options = new CsvOptions<char>
        {
            ShouldSkipRow = args => args.Line == 1 || args.Record[0] == '#',
        };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.RawRecord.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("7,8,9", enumerator.Current.RawRecord.ToString());
    }

    [Fact]
    public void Should_Return_Correct_HasHeader_in_Skip()
    {
        foreach (var _ in CsvReader.Enumerate("A,B,C\n1,2,3", GetOpts(true)))
        { }

        foreach (var _ in CsvReader.Enumerate("X,y,z\nX,y,z", GetOpts(false)))
        { }


        CsvOptions<char> GetOpts(bool hasHeader)
        {
            return new CsvOptions<char>
            {
                HasHeader = hasHeader,
                ShouldSkipRow = args =>
                {
                    if (args.Record[0] == 'A')
                    {
                        Assert.True(args.IsHeader);
                    }
                    if (args.Record[0] == '1')
                    {
                        Assert.False(args.IsHeader);
                    }
                    if (args.Record[0] == 'X')
                    {
                        Assert.False(args.IsHeader);
                    }

                    return false;
                }
            };
        }
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

        var options = new CsvOptions<char>
        {
            ShouldSkipRow = args => args.Line == 1 || args.Record[0] == '#',
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
