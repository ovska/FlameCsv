using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public static class AsciiVectorTests
{
    [Fact]
    public static void Should_Narrow_256()
    {
        Assert.SkipUnless(Vector128.IsHardwareAccelerated, "Vector128 not supported");

        Span<char> data = stackalloc char[Vector256<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        var vec = AsciiVector.Load256(ref data[0], 0);
        Assert.All(vec.ToArray(), b => Assert.True(b > 127));

        data.Fill(',');
        vec = AsciiVector.Load256(ref data[0], 0);
        Assert.Equal(new string(',', Vector256<byte>.Count), vec.ToAsciiString());
    }

    [Fact]
    public static void Should_Narrow_512()
    {
        Assert.SkipUnless(Vector512.IsHardwareAccelerated, "Vector512 not supported");

        Span<char> data = stackalloc char[Vector512<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        var vec = AsciiVector.Load512(ref data[0], 0);
        Assert.All(vec.ToArray(), b => Assert.True(b > 127));

        data.Fill(',');
        vec = AsciiVector.Load512(ref data[0], 0);
        Assert.Equal(new string(',', Vector512<byte>.Count), vec.ToAsciiString());
    }
}
