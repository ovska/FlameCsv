namespace FlameCsv.Tests;

public static class TokenTests
{
    [Fact]
    public static void Should_Return_Safe_Stackalloc()
    {
        Assert.True(Token<byte>.CanStackalloc(0));
        Assert.True(Token<byte>.CanStackalloc(256));
        Assert.True(Token<byte>.CanStackalloc(512));
        Assert.False(Token<byte>.CanStackalloc(513));
        Assert.False(Token<byte>.CanStackalloc(-1));

        Assert.True(Token<char>.CanStackalloc(0));
        Assert.True(Token<char>.CanStackalloc(256));
        Assert.False(Token<char>.CanStackalloc(257));
        Assert.False(Token<char>.CanStackalloc(-1));
    }

    [Fact]
    public static void Should_Return_LOH_Limit()
    {
        Assert.False(Token<byte>.LargeObjectHeapAllocates(10_000));
        Assert.False(Token<byte>.LargeObjectHeapAllocates(84_000));
        Assert.True(Token<byte>.LargeObjectHeapAllocates(84_001));

        Assert.False(Token<char>.LargeObjectHeapAllocates(10_000));
        Assert.False(Token<char>.LargeObjectHeapAllocates(42_000));
        Assert.True(Token<char>.LargeObjectHeapAllocates(42_001));

        Assert.False(Token<long>.LargeObjectHeapAllocates(10_000));
        Assert.True(Token<long>.LargeObjectHeapAllocates(20_000));
    }
}
