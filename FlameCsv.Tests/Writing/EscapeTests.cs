using FlameCsv.Tests.Utilities;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public static class EscapeTests
{
    private static RFC4180Escaper<char> GetEscaper(string newline = "\r\n")
    {
        return new RFC4180Escaper<char>(new CsvTextOptions
        {
            Delimiter = ',',
            Quote = '|',
            Newline = newline,
            Whitespace = "",
            Escape = null,
        });
    }

    [Fact]
    public static void Should_Partial_Escape_Larger_Destination()
    {
        ReadOnlySpan<char> input = "|t|e|s|t|";

        Assert.True(GetEscaper().NeedsEscaping(input, out int specialCount));
        Assert.Equal(5, specialCount);

        using var arrayPool = new ReturnTrackingArrayPool<char>();
        var writer = new ValueBufferWriter<char>();
        var destination = writer.GetSpan(14);
        input.CopyTo(destination);

        ((IEscaper<char>)GetEscaper()).EscapeField(
            in writer,
            source: destination[..input.Length],
            destination: destination,
            specialCount: specialCount,
            arrayPool: arrayPool);

        Assert.Equal("|||t||e||s||t|||", writer.Writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData(",test ", "|,test |")]
    [InlineData("test,", "|test,|")]
    [InlineData(",test", "|,test|")]
    [InlineData(" te|st ", "| te||st |")]
    [InlineData("|", "||||")]
    [InlineData("|t", "|||t|")]
    public static void Should_Partial_Escape(string input, string expected)
    {
        Assert.True(GetEscaper().NeedsEscaping(input, out int specialCount));

        var writer = new ValueBufferWriter<char>();
        Span<char> firstBuffer = writer.GetSpan(input.Length);
        input.CopyTo(firstBuffer);

        using var arrayPool = new ReturnTrackingArrayPool<char>();

        ((IEscaper<char>)GetEscaper()).EscapeField(
            in writer,
            source: firstBuffer,
            destination: firstBuffer,
            specialCount: specialCount,
            arrayPool: arrayPool);

        Assert.Equal(expected, writer.Writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData("", "||")]
    [InlineData(" ", "| |")]
    [InlineData("test", "|test|")]
    public static void Should_Wrap_In_Quotes(string input, string expected)
    {
        Span<char> buffer = stackalloc char[expected.Length];
        input.CopyTo(buffer);

        ((IEscaper<char>)GetEscaper()).EscapeField(buffer[..input.Length], buffer, 0);
        Assert.Equal(expected, buffer.ToString());
    }

    [Theory]
    [InlineData(",test", "|,test|")]
    [InlineData("test,", "|test,|")]
    [InlineData("\r\ntest", "|\r\ntest|")]
    [InlineData("te|st", "|te||st|")]
    [InlineData("||", "||||||")]
    [InlineData("|", "||||")]
    [InlineData("|a|", "|||a|||")]
    [InlineData("a|a", "|a||a|")]
    public static void Should_Escape(string input, string expected)
    {
        Assert.True(GetEscaper().NeedsEscaping(input, out int quoteCount));

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        ((IEscaper<char>)GetEscaper()).EscapeField(sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
    }

    [Theory]
    [InlineData("\ntest", "|\ntest|")]
    [InlineData("\n|test|", "|\n||test|||")]
    [InlineData("test\n", "|test\n|")]
    [InlineData("\n", "|\n|")]
    public static void Should_Escape_1Char_Newline(string input, string expected)
    {

        var escaper = GetEscaper("\n");
        Assert.True(escaper.NeedsEscaping(input, out int quoteCount));

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        ((IEscaper<char>)GetEscaper("\n")).EscapeField(sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
    }

    private static readonly (int? quoteCount, string data)[] _needsEscapingData =
    [
        (null, "foobar"),
        (null, "foo bar"),
        (null, "\r \r"),
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

    public static IEnumerable<object?[]> NeedsEscapingData() =>
        from x in _needsEscapingData
        from newline in new[] { "\r\n", "\n" }
        select new object?[] { newline, x.quoteCount, x.data };

    [Theory, MemberData(nameof(NeedsEscapingData))]
    public static void Should_Check_Needs_Escaping(string newline, int? quotes, string input)
    {
        var escaper = GetEscaper(newline);
        input = input.Replace("\r\n", newline);

        if (!quotes.HasValue)
        {
            Assert.False(escaper.NeedsEscaping(input, out _));
        }
        else
        {
            Assert.True(escaper.NeedsEscaping(input, out var quoteCount));
            Assert.Equal(quotes.Value, quoteCount);
        }
    }
}
