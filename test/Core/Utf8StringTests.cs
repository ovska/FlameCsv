using System.Text;

namespace FlameCsv.Tests;

public static class Utf8StringTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" ")]
    [InlineData("Hello, World!")]
    public static void Should_Work(string? value)
    {
        Utf8String utf8 = new(value);

        Assert.Equal(value.AsMemory(), utf8.AsMemory<char>());
        Assert.Equal(Encoding.UTF8.GetBytes(value ?? "").AsMemory(), utf8.AsMemory<byte>());

        Assert.Equal(value.AsSpan(), utf8.AsSpan<char>());
        Assert.Equal(Encoding.UTF8.GetBytes(value ?? "").AsSpan(), utf8.AsSpan<byte>());
    }

    [Fact]
    public static void Should_Throw_On_Invalid_Token()
    {
        Utf8String utf8 = new("Hello, World!");
        Assert.Throws<NotSupportedException>(() => utf8.AsMemory<short>());
        Assert.Throws<NotSupportedException>(() => utf8.AsSpan<short>());
    }
}
