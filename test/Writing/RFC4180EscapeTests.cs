using FlameCsv.Writing.Escaping;

namespace FlameCsv.Tests.Writing;

public static class RFC4180EscapeTests
{
    public static CsvOptions<char> Options { get; }

    static RFC4180EscapeTests()
    {
        Options = new() { Delimiter = ',', Quote = '|' };
        Options.MakeReadOnly();
    }

    [Theory]
    [InlineData("", "||")]
    [InlineData(" ", "| |")]
    [InlineData("test", "|test|")]
    public static void Should_Wrap_In_Quotes(string input, string expected)
    {
        Span<char> buffer = stackalloc char[expected.Length];
        input.CopyTo(buffer);

        Escape.Scalar('|', buffer[..input.Length], buffer, 0);
        Assert.Equal(expected, buffer.ToString());
    }

    private static (string input, string expected)[] _data =
    [
        (",test", "|,test|"),
        ("test,", "|test,|"),
        ("\r\ntest", "|\r\ntest|"),
        ("te|st", "|te||st|"),
        ("||", "||||||"),
        ("|", "||||"),
        ("|a|", "|||a|||"),
        ("a|a", "|a||a|"),
    ];

    public static TheoryData<string, string, PoisonPagePlacement> EscapeData() =>
        [
            .. from tuple in _data
            from placement in GlobalData.PoisonPlacement
            select (tuple.input, tuple.expected, placement),
        ];

    [Theory, MemberData(nameof(EscapeData))]
    public static void Should_Escape(string input, string expected, PoisonPagePlacement placement)
    {
        Assert.True(input.AsSpan().ContainsAny(Options.NeedsQuoting));
        int quoteCount = input.AsSpan().Count('|');

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        using var owner = BoundedMemory.AllocateLoose<char>(expectedLength, placement);
        Span<char> sharedBuffer = owner.Memory.Span[..expectedLength];
        input.CopyTo(sharedBuffer);

        Escape.Scalar('|', sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal($"|{input.Replace("|", "||")}|", sharedBuffer.ToString());
    }

    [Theory]
    [InlineData("\ntest", "|\ntest|")]
    [InlineData("\n|test|", "|\n||test|||")]
    [InlineData("test\n", "|test\n|")]
    [InlineData("\n", "|\n|")]
    public static void Should_Escape_1Char_Newline(string input, string expected)
    {
        Assert.True(input.AsSpan().ContainsAny(Options.NeedsQuoting));
        int quoteCount = input.AsSpan().Count('|');

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        Escape.Scalar('|', sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal($"|{input.Replace("|", "||")}|", sharedBuffer.ToString());
    }

    private static readonly (int? quoteCount, string data)[] _needsEscapingData =
    [
        (null, "foobar"),
        (null, "foo bar"),
        (0, "foo,bar"),
        (1, "foo|bar"),
        (2, "|foobar|"),
        (1, "|"),
        (0, "\r\n"),
        (0, "\r\n,"),
        (0, "Really, really long input"),
        (2, "| test |"),
        (3, "||\r\n test |"),
    ];

    public static TheoryData<CsvNewline, int?, string> NeedsEscapingData()
    {
        var values =
            from x in _needsEscapingData
            from newline in new[] { CsvNewline.CRLF, CsvNewline.LF }
            select new
            {
                newline,
                x.quoteCount,
                x.data,
            };

        var theory = new TheoryData<CsvNewline, int?, string>();

        foreach (var x in values)
        {
            theory.Add(x.newline, x.quoteCount, x.data);
        }

        return theory;
    }

    [Theory, MemberData(nameof(NeedsEscapingData))]
    public static void Should_Check_Needs_Escaping(CsvNewline newline, int? quotes, string input)
    {
        var options = new CsvOptions<char> { Quote = '|', Newline = newline };
        input = input.Replace("\r\n", newline.AsString());

        options.MakeReadOnly();
        bool needsQuoting = input.AsSpan().ContainsAny(options.NeedsQuoting);

        if (!quotes.HasValue)
        {
            Assert.False(needsQuoting);
        }
        else
        {
            Assert.True(needsQuoting);
            Assert.Equal(quotes.Value, input.AsSpan().Count('|'));
        }
    }
}
