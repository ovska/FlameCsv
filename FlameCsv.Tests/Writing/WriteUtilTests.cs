using CommunityToolkit.HighPerformance;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public static class WriteUtilTests
{
    [Theory]
    [InlineData(" test ", "| test |")]
    [InlineData("test ", "|test |")]
    [InlineData(" test", "| test|")]
    [InlineData("te|st", "|te||st|")]
    [InlineData("||", "||||||")]
    [InlineData("|a|", "|||a|||")]
    [InlineData("a|a", "|a||a|")]
    public static void Should_Escape(string input, string expected)
    {
        if (!WriteUtil.NeedsEscaping(input, '|', out int quoteCount, out int escapedLength))
        {
            Assert.Equal(0, input.Count('|'));
            Assert.True(WriteUtil.NeedsEscaping(input.AsSpan(), stackalloc char[] { ' ' }, out escapedLength));
        }

        Assert.Equal(expected.Length, escapedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[escapedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        WriteUtil.Escape(sharedBuffer[..input.Length], sharedBuffer, '|', quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));
    }

    [Theory]
    [InlineData(-1, "", 0)]
    [InlineData(-1, "test", 0)]
    [InlineData(4, "|", 1)]
    [InlineData(10, "|test|", 2)]
    public static void Should_Return_Escaped_Quoted(int expected, string input, int expectedQuotes)
    {
        if (expected >= 0)
        {
            Assert.True(WriteUtil.NeedsEscaping(input, '|', out int quoteCount, out int escapedLength));
            Assert.Equal(expected, escapedLength);
            Assert.Equal(expectedQuotes, quoteCount);
        }
        else
        {
            Assert.False(WriteUtil.NeedsEscaping(input, '|', out _, out _));
        }
    }

    [Theory]
    [InlineData(-1, "")]
    [InlineData(-1, "te st")]
    [InlineData(7, " test")]
    [InlineData(7, "test ")]
    [InlineData(9, " te st ")]
    public static void Should_Return_Escaped_Whitespace(int expected, string input)
    {
        ReadOnlySpan<char> whitespace = stackalloc char[] { ' ' };
        if (expected >= 0)
        {
            Assert.True(WriteUtil.NeedsEscaping(input, whitespace, out int escapedLength));
            Assert.Equal(expected, escapedLength);
        }
        else
        {
            Assert.False(WriteUtil.NeedsEscaping(input, whitespace, out _));
        }
    }
}
