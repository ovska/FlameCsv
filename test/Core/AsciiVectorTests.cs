using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public static class AsciiVectorTests
{
    [Fact]
    public static void Should_Zero_Lower_128()
    {
        Assert.SkipUnless(Vector128.IsHardwareAccelerated, "Vector128 not supported");

        Span<byte> actual = stackalloc byte[Vector128<byte>.Count];
        Span<byte> expected = stackalloc byte[Vector128<byte>.Count];

        for (int i = 0; i <= Vector128<byte>.Count; i++)
        {
            Vector128<byte> result = AsciiVector.ZeroLower2(Vector128<byte>.AllBitsSet, i);
            result.CopyTo(actual);

            expected.Clear();
            expected.Slice(i).Fill((byte)0xFF);

            Assert.Equal(expected, actual);
        }
    }

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
        Assert.SkipUnless(Avx512BW.IsSupported, "AVX-512 not supported");

        Span<char> data = stackalloc char[Vector512<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        var vec = AsciiVector.Load512(ref data[0], 0);
        Assert.All(vec.ToArray(), b => Assert.True(b > 127));

        data.Fill(',');
        vec = AsciiVector.Load512(ref data[0], 0);
        Assert.Equal(new string(',', Vector512<byte>.Count), vec.ToAsciiString());
    }
}
