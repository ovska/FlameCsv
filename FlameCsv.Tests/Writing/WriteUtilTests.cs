using System.Buffers;
using CommunityToolkit.HighPerformance;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public static class WriteUtilTests
{
    [Theory]
    [InlineData(" test ", "| test", " |")]
    [InlineData("test ", "|test", " |")]
    [InlineData(" test", "| tes", "t|")]
    [InlineData(" te|st ", "| te||s", "t |")]
    [InlineData("|", "|", "|||")]
    [InlineData("|t", "||", "|t|")]
    public static void Should_Partial_Escape(string input, string first, string second)
    {
        if (!WriteUtil.NeedsEscaping(input, '|', out int quoteCount, out int escapedLength))
        {
            Assert.Equal(0, input.Count('|'));
            Assert.True(WriteUtil.NeedsEscaping(input.AsSpan(), new[] { ' ' }, out escapedLength));
        }

        // The escape should work despite src and dst sharing a memory region
        Assert.Equal(input.Length, first.Length);
        Span<char> firstBuffer = stackalloc char[first.Length];
        // input.CopyTo(firstBuffer);

        char[]? array = null;

        WriteUtil.PartialEscape(
            source: input.AsSpan(),//firstBuffer,
            destination: firstBuffer,
            quote: '|',
            requiredLength: escapedLength,
            quoteCount: quoteCount,
            array: ref array,
            overflowLength: out int overflowLength);

        Assert.NotNull(array);
        Assert.Equal(first, firstBuffer.ToString());
        Assert.Equal(second, array.AsSpan(0, overflowLength).ToString());

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            firstBuffer.ToString() + array.AsSpan(0, overflowLength).ToString());

        ArrayPool<char>.Shared.Return(array!);
    }

    [Theory]
    [InlineData(" test ", "| test |")]
    [InlineData("test ", "|test |")]
    [InlineData(" test", "| test|")]
    [InlineData("te|st", "|te||st|")]
    [InlineData("||", "||||||")]
    [InlineData("|", "||||")]
    [InlineData("|a|", "|||a|||")]
    [InlineData("a|a", "|a||a|")]
    public static void Should_Escape(string input, string expected)
    {
        if (!WriteUtil.NeedsEscaping(input, '|', out int quoteCount, out int escapedLength))
        {
            Assert.Equal(0, input.Count('|'));
            Assert.True(WriteUtil.NeedsEscaping(input.AsSpan(), new[] { ' ' }, out escapedLength));
        }

        Assert.Equal(expected.Length, escapedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[escapedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        WriteUtil.Escape(sharedBuffer[..input.Length], sharedBuffer, '|', quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
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
    [InlineData(-1, "test")]
    [InlineData(-1, "te st")]
    [InlineData(7, " test")]
    [InlineData(7, "test ")]
    [InlineData(9, " te st ")]
    public static void Should_Return_Escaped_Whitespace(int expected, string input)
    {
        ReadOnlyMemory<char> whitespace = " ".AsMemory();
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
