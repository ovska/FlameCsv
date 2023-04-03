using System.Buffers;
using FlameCsv.Reading;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public static class WriteUtilTests
{
    private static readonly CsvDialect<char> _tokens = new(
        delimiter: ',',
        quote: '|',
        newline: "\r\n".AsMemory(),
        whitespace: " ".AsMemory());

    [Fact]
    public static void Should_Partial_Escape_Larger_Destination()
    {
        ReadOnlySpan<char> input = "|t|e|s|t|";
        Span<char> destination = stackalloc char[14];

        Assert.True(WriteUtil.NeedsEscaping(input, in _tokens, out int quoteCount));
        Assert.Equal(5, quoteCount);

        input.CopyTo(destination);
        char[] originalArrayRef = new char[2];
        char[]? array = originalArrayRef;

        WriteUtil.PartialEscape(
            source: destination[..input.Length],
            destination: destination,
            quote: '|',
            quoteCount: quoteCount,
            new ValueBufferOwner<char>(ref array, ArrayPool<char>.Shared));

        Assert.Equal("|||t||e||s||t|", destination.ToString());
        Assert.Equal("||", array.AsSpan().ToString());
        Assert.Same(originalArrayRef, array);
    }

    [Theory]
    [InlineData(",test ", "|,test", " |")]
    [InlineData("test,", "|test", ",|")]
    [InlineData(",test", "|,tes", "t|")]
    [InlineData(" te|st ", "| te||s", "t |")]
    [InlineData("|", "|", "|||")]
    [InlineData("|t", "||", "|t|")]
    public static void Should_Partial_Escape(string input, string first, string second)
    {
        Assert.True(WriteUtil.NeedsEscaping(input, in _tokens, out int quoteCount));

        // The escape is designed to work despite src and dst sharing a memory region
        Assert.Equal(input.Length, first.Length);
        Span<char> firstBuffer = stackalloc char[first.Length];
        input.CopyTo(firstBuffer);

        char[]? array = null;

        WriteUtil.PartialEscape(
            source: firstBuffer,
            destination: firstBuffer,
            quote: '|',
            quoteCount: quoteCount,
            new ValueBufferOwner<char>(ref array, ArrayPool<char>.Shared));

        int overflowLength = quoteCount + 2;

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
        Assert.True(WriteUtil.NeedsEscaping(input, in _tokens, out int quoteCount));

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        WriteUtil.Escape(sharedBuffer[..input.Length], sharedBuffer, '|', quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
    }

    private static readonly (int? quoteCount, string data)[] _needsEscapingData =
    {
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
    };

    public static IEnumerable<object?[]> NeedsEscapingData() =>
        from x in _needsEscapingData
        from newline in new[] { "\r\n", "\n" }
        select new object?[] { newline, x.quoteCount, x.data };

    [Theory, MemberData(nameof(NeedsEscapingData))]
    public static void Should_Check_Needs_Escaping(string newline, int? quotes, string input)
    {
        var tokens = _tokens.Clone(newline: newline.AsMemory());
        input = input.Replace("\r\n", newline);

        if (!quotes.HasValue)
        {
            Assert.False(WriteUtil.NeedsEscaping(input, in tokens, out _));
        }
        else
        {
            Assert.True(WriteUtil.NeedsEscaping(input, in tokens, out var quoteCount));
            Assert.Equal(quotes.Value, quoteCount);
        }
    }
}
