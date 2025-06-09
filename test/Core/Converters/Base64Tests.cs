using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class Base64Tests
{
    [Fact]
    public static void Should_Not_Convert_Invalid()
    {
        Assert.False(Base64TextConverter.Instance.TryParse("invalid base64", out _));
        Assert.False(Base64TextConverter.Instance.TryFormat([], new ArraySegment<byte>([1]), out _));
        Assert.False(Base64Utf8Converter.Instance.TryParse("invalid base64"u8, out _));
        Assert.False(Base64Utf8Converter.Instance.TryFormat([], new ArraySegment<byte>([1]), out _));
    }
}
