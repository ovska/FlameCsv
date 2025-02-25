using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv.Tests;

public static class Utf8StringTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("\n", true)]
    [InlineData("\r\n", true)]
    [InlineData(" ", true)]
    [InlineData("Hello, World!", false)]
    public static void Should_Cache_Common_Values(string? value, bool cached)
    {
        Utf8String utf8 = value;
        Assert.Equal(value ?? "", utf8);
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)utf8, out var segment1));
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)(Utf8String)value, out var segment2));

        if (cached)
        {
            Assert.Same(utf8, (Utf8String)value);
            Assert.Same(segment1.Array, segment2.Array);
        }
        else
        {
            Assert.NotSame(utf8, (Utf8String)value);
            Assert.NotSame(segment1.Array, segment2.Array);
        }

        Assert.Equal((ReadOnlyMemory<byte>)utf8, Encoding.UTF8.GetBytes(value ?? ""));
    }
}
