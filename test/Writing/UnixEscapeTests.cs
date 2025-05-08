using FlameCsv.Writing.Escaping;

namespace FlameCsv.Tests.Writing;

file static class EscapeExt
{
    public static readonly CsvOptions<char> Options = new()
    {
        Delimiter = ',',
        Quote = '|',
        Escape = '^',
    };

    public static bool MustBeQuoted(this UnixEscaper<char> escaper, ReadOnlySpan<char> field, out int specialCount)
    {
        if (field.IndexOfAny(Options.NeedsQuoting) >= 0)
        {
            specialCount = escaper.CountEscapable(field);
            return true;
        }

        specialCount = 0;
        return false;
    }
}

public static class UnixEscapeTests
{
    private static readonly UnixEscaper<char> _escaper = new(
        quote: EscapeExt.Options.Quote,
        escape: EscapeExt.Options.Escape!.Value
    );

    [Theory]
    [InlineData("", "||")]
    [InlineData(" ", "| |")]
    [InlineData("test", "|test|")]
    public static void Should_Wrap_In_Quotes(string input, string expected)
    {
        Span<char> buffer = stackalloc char[expected.Length];
        input.CopyTo(buffer);

        var escaper = _escaper;
        Escape.Scalar(ref escaper, buffer[..input.Length], buffer, 0);
        Assert.Equal(expected, buffer.ToString());
    }

    [Theory]
    [InlineData(",test", "|,test|")]
    [InlineData("test,", "|test,|")]
    [InlineData("\r\ntest", "|\r\ntest|")]
    [InlineData("te|st", "|te^|st|")]
    [InlineData("||", "|^|^||")]
    [InlineData("|", "|^||")]
    [InlineData("|a|", "|^|a^||")]
    [InlineData("a|a", "|a^|a|")]
    public static void Should_Escape(string input, string expected)
    {
        Assert.True(_escaper.MustBeQuoted(input, out int quoteCount));

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if the source and destination share a memory region
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        var escaper = _escaper;
        Escape.Scalar(ref escaper, sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal($"|{input.Replace("|", "^|")}|", sharedBuffer.ToString());
    }

    [Theory]
    [InlineData("\ntest", "|\ntest|")]
    [InlineData("\n|test|", "|\n^|test^||")]
    [InlineData("test\n", "|test\n|")]
    [InlineData("\n", "|\n|")]
    public static void Should_Escape_1Char_Newline(string input, string expected)
    {
        var escaper = _escaper;
        int quoteCount = escaper.CountEscapable(input);

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        Escape.Scalar(ref escaper, sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal($"|{input.Replace("|", "^|")}|", sharedBuffer.ToString());
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
        var options = new CsvOptions<char>
        {
            Quote = '|',
            Escape = '^',
            Newline = newline,
        };
        input = input.Replace("\r\n", newline.AsString());

        bool needsQuoting = input.AsSpan().ContainsAny(options.NeedsQuoting);

        if (!quotes.HasValue)
        {
            Assert.False(needsQuoting);
        }
        else
        {
            Assert.True(needsQuoting);
            Assert.Equal(quotes.Value, _escaper.CountEscapable(input));
        }
    }
}
