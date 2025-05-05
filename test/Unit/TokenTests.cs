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
}
