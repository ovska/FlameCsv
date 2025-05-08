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
        Utf8String utf8 = value;
        Assert.Equal(value ?? "", utf8);
        Assert.Equal(utf8.GetBytes(), Encoding.UTF8.GetBytes(value ?? ""));
    }
}
