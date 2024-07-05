using FlameCsv.Converters;

namespace FlameCsv.Tests;

public class CsvOptionsTests
{
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

        Assert.Equal(['\r', '\n'], options.GetNewlineSpan(['\0', '\0']));
        Assert.Null(options.Newline);

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

        Assert.Equal([(byte)'\r', (byte)'\n'], options.GetNewlineSpan([0, 0]));
        Assert.Null(options.Newline);

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
        Assert.True(options.MakeReadOnly());
        Assert.False(options.MakeReadOnly());

        Run(o => o.Delimiter = default);
        Run(o => o.Quote = default);
        Run(o => o.Escape = default);
        Run(o => o.Newline = "");
        Run(o => o.Whitespace = "");
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
        Run(o => o.StringPool = null);
        Run(o => o.IgnoreEnumCase = default);
        Run(o => o.AllowContentInExceptions = default);
        Run(o => o.AllowUndefinedEnumValues = default);
        Run(o => o.UseDefaultConverters = default);
        Run(o => o.EnumFormat = "");
        Run(o => o.FieldEscaping = default);
        Run(o => o.FormatProvider = default);
        Run(o => o.FormatProviders.Add(typeof(int), null));
        Run(o => o.Formats.Add(typeof(int), null));
        Run(o => o.NoLineBuffering = true);
        Run(o => o.Null = "null");
        Run(o => o.ValidateFieldCount = default);
        Run(o => o.BooleanValues.Add(default));

        void Run(Action<CsvOptions<char>> action)
        {
            Assert.Throws<InvalidOperationException>(() => action(options));
        }
    }

    [Fact]
    public void Should_Validate_Field_Count()
    {
        const string data = "1,1,1\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvOptions<char> { ValidateFieldCount = true, HasHeader = false };

        using var enumerator = CsvReader.Enumerate(data, options).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Throws<InvalidDataException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Should_Validate_Field_Count_With_Header()
    {
        const string data = "A,B,C\r\n1,1,1\r\n1,1,1,1\r\n";

        var options = new CsvOptions<char> { ValidateFieldCount = true, HasHeader = true };

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

        var options = new CsvOptions<char>
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
                ShouldSkipRow = (in CsvRecordSkipArgs<char> args) =>
                {
                    if (args.Record.Span[0] == 'A')
                    {
                        Assert.False(args.HeaderRead);
                    }
                    if (args.Record.Span[0] == '1')
                    {
                        Assert.True(args.HeaderRead);
                    }
                    if (args.Record.Span[0] == 'X')
                    {
                        Assert.Null(args.HeaderRead);
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
