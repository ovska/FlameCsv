using System.Globalization;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Exceptions;

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
    public static void Should_Validate_Args()
    {
        // null
        Run(o => o.Delimiter = '\0');
        Run(o => o.Quote = '\0');

        // space
        Run(o => o.Delimiter = ' ');
        Run(o => o.Quote = ' ');

        // CR
        Run(o => o.Delimiter = '\r');
        Run(o => o.Quote = '\r');

        // LF
        Run(o => o.Delimiter = '\n');
        Run(o => o.Quote = '\n');

        // not ascii
        Run(o => o.Delimiter = (char)128);
        Run(o => o.Quote = (char)128);

        // alphanumeric or minus
        Run(o => o.Delimiter = 'a');
        Run(o => o.Quote = 'A');
        Run(o => o.Delimiter = '1');
        Run(o => o.Quote = '-');

        // invalid enum
        Run(o => o.Newline = (1 + CsvNewline.Platform));

        // enum sep
        Run(o => o.EnumFlagsSeparator = '\0'); // null
        Run(o => o.EnumFlagsSeparator = ' '); // whitespace
        Run(o => o.EnumFlagsSeparator = '-'); // minus
        Run(o => o.EnumFlagsSeparator = '\r'); // CR
        Run(o => o.EnumFlagsSeparator = '\n'); // LF
        Run(o => o.EnumFlagsSeparator = '1'); // digit
        Run(o => o.EnumFlagsSeparator = 'a'); // letter
        Run(o => o.EnumFlagsSeparator = 'A'); // letter
        Run(o => o.EnumFlagsSeparator = '\ud800'); // high surrogate
        Run(o => o.EnumFlagsSeparator = '\udc00'); // low surrogate

        // bytes need ascii
        Assert.ThrowsAny<ArgumentException>(() => new CsvOptions<byte> { EnumFlagsSeparator = 'ä' });
        _ = new CsvOptions<char> { EnumFlagsSeparator = 'ä' };

        Assert.Throws<CsvConfigurationException>(() => new CsvOptions<char> { Delimiter = '"' }.Validate());
        Assert.Throws<CsvConfigurationException>(() => new CsvOptions<char> { Quote = ',' }.Validate());

        static void Run(Action<CsvOptions<char>> action)
        {
            Assert.ThrowsAny<ArgumentException>(() => action(new CsvOptions<char>()));
        }
    }

    [Fact]
    public static void Should_Initialize_NeedsQuoting()
    {
        var def = CsvOptions<char>.Default;
        Assert.True(def.NeedsQuoting.Contains(def.Delimiter));
        Assert.True(def.NeedsQuoting.Contains(def.Quote));
        Assert.True(def.NeedsQuoting.Contains('\r'));
        Assert.True(def.NeedsQuoting.Contains('\n'));
        Assert.False(def.NeedsQuoting.Contains('a'));
        Assert.False(def.NeedsQuoting.Contains('\\'));
        Assert.Same(def.NeedsQuoting, def.NeedsQuoting); // cached
        Assert.Same(def.NeedsQuoting, def.NeedsQuotingChar); // same instance

        // ensure CR is quoted as well <--- why?
        var withSingleTokenNewline = new CsvOptions<char> { Newline = CsvNewline.LF };
        withSingleTokenNewline.MakeReadOnly();
        Assert.True(withSingleTokenNewline.NeedsQuoting.Contains('\n'));
        Assert.True(withSingleTokenNewline.NeedsQuoting.Contains('\r')); // both always added

        var byteOpts = new CsvOptions<byte> { Delimiter = ';', Quote = '\'' };
        byteOpts.MakeReadOnly();

        Assert.True(byteOpts.NeedsQuoting.Contains((byte)';'));
        Assert.True(byteOpts.NeedsQuoting.Contains((byte)'\''));

        Assert.True(byteOpts.NeedsQuotingChar.Contains(';'));
        Assert.True(byteOpts.NeedsQuotingChar.Contains('\''));
    }

    [Fact]
    public static void Should_Return_Null_Token()
    {
        var to = new CsvOptions<char>
        {
            NullTokens = { [typeof(long)] = "long", [typeof(short?)] = "short" },
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
            NullTokens = { [typeof(long)] = "long", [typeof(short?)] = "short" },
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
            },
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
            },
        };

        Assert.True(bo.GetConverter<int>().TryParse("1  "u8, out _));
        Assert.False(bo.GetConverter<long>().TryParse("1  "u8, out _));
        Assert.False(bo.GetConverter<int>().TryParse("  1"u8, out _));
        Assert.True(bo.GetConverter<long>().TryParse("  1"u8, out _));
    }

    [Theory, InlineData(typeof(int*)), InlineData(typeof(List<>)), InlineData(typeof(Span<int>)), InlineData(null)]
    public static void Should_Validate_TypeDictionary_Argument(Type? type)
    {
        var to = new CsvOptions<char>();
        Assert.ThrowsAny<ArgumentException>(() => to.NullTokens[type!] = "");

        var bo = new CsvOptions<byte>();
        Assert.ThrowsAny<ArgumentException>(() => bo.NullTokens[type!] = "");
    }

    [Fact]
    public void Should_Not_Use_Builtin_Converters()
    {
        var options = new CsvOptions<char> { UseDefaultConverters = false };
        Assert.ThrowsAny<CsvConverterMissingException>(() => options.GetConverter<int>());
    }

    [Fact]
    public void Should_Return_Text_Defaults()
    {
        var options = CsvOptions<char>.Default;
        Assert.True(options.IsReadOnly);

        Assert.Equal(CsvNewline.CRLF, options.Newline);

        var boolParser = options.GetConverter<bool>();
        Assert.True(boolParser.TryParse("true", out var bValue));
        Assert.True(bValue);

        var intParser = options.GetConverter<ushort>();
        Assert.True(intParser.TryParse("1234", out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetConverter<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday", out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.ThrowsAny<CsvConverterMissingException>(() => options.GetConverter<Type>());

        Assert.Same(options, CsvOptions<char>.Default);
    }

    [Fact]
    public void Should_Return_Utf8_Defaults()
    {
        var options = CsvOptions<byte>.Default;
        Assert.True(options.IsReadOnly);
        Assert.Same(options, CsvOptions<byte>.Default);

        Assert.Equal(CsvNewline.CRLF, options.Newline);

        var boolParser = options.GetConverter<bool>();
        Assert.True(boolParser.TryParse("true"u8, out var bValue));
        Assert.True(bValue);

        var intParser = options.GetConverter<ushort>();
        Assert.True(intParser.TryParse("1234"u8, out var iValue));
        Assert.Equal(1234, iValue);

        var nullEnumParser = options.GetConverter<DayOfWeek>();
        Assert.True(nullEnumParser.TryParse("Monday"u8, out var mndy));
        Assert.Equal(DayOfWeek.Monday, mndy);

        Assert.ThrowsAny<CsvConverterMissingException>(() => options.GetConverter<Type>());
    }

    [Fact]
    public void Should_Throw_On_ReadOnly_Modified()
    {
        var options = new CsvOptions<char>();
        options.MakeReadOnly();

        Run(o => o.Delimiter = ',');
        Run(o => o.Quote = '"');
        Run(o => o.Newline = CsvNewline.Platform);
        Run(o => o.Trimming = CsvFieldTrimming.Leading);
        Run(o => o.RecordCallback = null);
        Run(o => o.NullTokens[typeof(int)] = "");
        Run(o => o.HasHeader = false);
        Run(o => o.Comparer = StringComparer.Ordinal);
        Run(o => o.Converters[0] = new BooleanTextConverter());
        Run(o => o.Converters.Add(new BooleanTextConverter()));
        Run(o => o.Converters.Insert(0, new BooleanTextConverter()));
        Run(o => o.Converters.Remove(new BooleanTextConverter()));
        Run(o => o.Converters.RemoveAt(0));
        Run(o => o.Converters.Clear());
        Run(o => o.UseDefaultConverters = false);
        Run(o => o.IgnoreDuplicateHeaders = true);
        Run(o => o.IgnoreUnmatchedHeaders = true);
        Run(o => o.EnumFormat = "");
        Run(o => o.EnumFlagsSeparator = '^');
        Run(o => o.AllowUndefinedEnumValues = false);
        Run(o => o.IgnoreEnumCase = false);
        Run(o => o.FieldQuoting = default);
        Run(o => o.FormatProvider = null);
        Run(o => o.FormatProviders.Add(typeof(int), null));
        Run(o => o.Formats.Add(typeof(int), null));
        Run(o => o.Null = "null");
        Run(o => o.NumberStyles.Add(typeof(int), NumberStyles.None));
        Run(o => o.BooleanValues.Add(default));
        Run(o => o.TypeBinder = new CsvReflectionBinder<char>(o));

        void Run(Action<CsvOptions<char>> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
    }

    [Fact]
    public void Should_ReRead_Header()
    {
        const string data = """
            A,B,C
            1,2,3

            C,A,B
            3,1,2

            """;

        static void Callback(ref readonly CsvRecordCallbackArgs<char> args)
        {
            if (args.Line == 1)
            {
                Assert.Empty(args.Header.ToArray());
            }
            else if (args.Line == 2)
            {
                Assert.Equal(["A", "B", "C"], args.Header);
            }
            else if (args.Line == 5)
            {
                Assert.Equal(["C", "A", "B"], args.Header);
            }

            if (args.IsEmpty)
            {
                args.SkipRecord = true;
                args.HeaderRead = false;
            }
        }

        CsvOptions<char> options = new() { HasHeader = true, RecordCallback = Callback };

        using (var enumerator = Csv.From(data).Enumerate(options).GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal("1", enumerator.Current.GetField("A").ToString());
            Assert.Equal("2", enumerator.Current.GetField("B").ToString());
            Assert.Equal("3", enumerator.Current.GetField("C").ToString());

            Assert.True(enumerator.MoveNext());
            Assert.Equal("1", enumerator.Current.GetField("A").ToString());
            Assert.Equal("2", enumerator.Current.GetField("B").ToString());
            Assert.Equal("3", enumerator.Current.GetField("C").ToString());

            Assert.False(enumerator.MoveNext());
        }

        var values = Csv.From(data).Read<Skippable>(options).ToList();
        Assert.Equal(2, values.Count);
        Assert.Equal(1, values[0].A);
        Assert.Equal(2, values[0].B);
        Assert.Equal(3, values[0].C);
        Assert.Equal(1, values[1].A);
        Assert.Equal(2, values[1].B);
        Assert.Equal(3, values[1].C);
    }

    [Fact]
    public void Should_Throw_If_Wrong_Token_Type()
    {
        Assert.Throws<NotSupportedException>(() => new CsvOptions<int>());
    }

    [Fact]
    public void Should_Skip_Rows()
    {
        const string data = "sep=,\r\n" + "A,B,C\r\n" + "1,2,3\r\n" + "#4,5,6\r\n" + "7,8,9\r\n";

        static void Callback(ref readonly CsvRecordCallbackArgs<char> args)
        {
            Assert.False(args.SkipRecord);

            if (args.Line == 1 || args.Record.Raw[0] == '#')
            {
                args.SkipRecord = true;
                Assert.True(args.SkipRecord);
            }
        }

        // record
        var options = new CsvOptions<char> { RecordCallback = Callback };

        using var enumerator = Csv.From(data).Enumerate(options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,2,3", enumerator.Current.Raw.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("7,8,9", enumerator.Current.Raw.ToString());

        // values
        var values = Csv.From(data).Read<Skippable>(options).ToList();

        Assert.Equal(2, values.Count);
        Assert.Equal(1, values[0].A);
        Assert.Equal(2, values[0].B);
        Assert.Equal(3, values[0].C);
        Assert.Equal(7, values[1].A);
        Assert.Equal(8, values[1].B);
        Assert.Equal(9, values[1].C);
    }

    [Fact]
    public void Should_Return_Correct_HeaderRead_in_Skip()
    {
        foreach (var _ in Csv.From("A,B,C\n1,2,3").Enumerate(GetOpts(true))) { }

        foreach (var _ in Csv.From("X,y,z\nX,y,z").Enumerate(GetOpts(false))) { }

        CsvOptions<char> GetOpts(bool hasHeader)
        {
            void Callback(ref readonly CsvRecordCallbackArgs<char> args)
            {
                Assert.Equal(args.Options.HasHeader, hasHeader);
                Assert.Equal(3, args.Record.FieldCount);

                if (args.Record.Raw[0] == 'A')
                {
                    Assert.Equal(1, args.Line);
                    Assert.False(args.HeaderRead);
                    Assert.Equal(0, args.Position);
                }

                if (args.Record.Raw[0] == '1')
                {
                    Assert.Equal(2, args.Line);
                    Assert.True(args.HeaderRead);
                }

                if (args.Record.Raw[0] == 'X')
                {
                    Assert.InRange(args.Line, 1, 2);
                    Assert.False(args.HeaderRead);
                }
            }

            return new CsvOptions<char> { HasHeader = hasHeader, RecordCallback = Callback };
        }
    }

    private sealed class Skippable
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }
}
