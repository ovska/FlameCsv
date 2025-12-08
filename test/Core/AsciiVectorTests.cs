using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
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

        // spray & pray test
        byte[] bytes = new byte[Vector256<byte>.Count * sizeof(char)];
        byte[] buffer = new byte[Vector256<byte>.Count];

        for (int i = 0; i < 1024; i++)
        {
            Random.Shared.NextBytes(bytes);
            vec = AsciiVector.Load256(ref Unsafe.As<byte, char>(ref bytes[0]), 0);
            vec.CopyTo(buffer);
            Assert.All(
                buffer,
                (b, i) =>
                {
                    if (b != bytes[i * 2])
                    {
                        Assert.True(b is 0 or >= 128, $"Expected 0 or 255 but got {b} at index {i}");
                    }
                }
            );
        }
    }

    [Fact]
    public static void Should_Narrow_512()
    {
        Assert.SkipUnless(Vector512.IsHardwareAccelerated || AdvSimd.IsSupported, "Vector512 not supported");

        Span<char> data = stackalloc char[Vector512<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        var vec = AsciiVector.Load512(ref data[0], 0);
        Assert.All(vec.ToArray(), b => Assert.True(b > 127));

        data.Fill(',');
        vec = AsciiVector.Load512(ref data[0], 0);
        Assert.Equal(new string(',', Vector512<byte>.Count), vec.ToAsciiString());

        // spray & pray test
        byte[] bytes = new byte[Vector512<byte>.Count * sizeof(char)];
        byte[] buffer = new byte[Vector512<byte>.Count];

        for (int i = 0; i < 1024; i++)
        {
            Random.Shared.NextBytes(bytes);
            vec = AsciiVector.Load512(ref Unsafe.As<byte, char>(ref bytes[0]), 0);
            vec.CopyTo(buffer);
            Assert.All(
                buffer,
                (b, i) =>
                {
                    if (b != bytes[i * 2])
                    {
                        Assert.True(
                            b is 0 or >= 128,
                            $"Expected 0 or 255 but got {b:x2} at index {i} (input was {bytes[i]:x2})"
                        );
                    }
                }
            );
        }
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("00100", true)]
    [InlineData("00010000000000000000000000000", true)]
    [InlineData("100000000000000000000000000000", true)]
    [InlineData("00000000000000000000000000010", true)]
    [InlineData("00010000000000000000000001000", false)]
    [InlineData("10010000000000000000000000000", false)]
    [InlineData("00000000000000000000000001001", false)]
    public static void Should_Return_Zero_or_One_Matches(string input, bool expected)
    {
        Span<char> data = stackalloc char[Vector512<byte>.Count];
        data.Fill('0');
        input.CopyTo(data);

        Vector256<byte> vec = Vector256.Equals(AsciiVector.Load256(ref data[0], 0), Vector256.Create((byte)'1'));
        var result = AsciiVector.ZeroOrOneMatches(vec);
        Assert.Equal(expected, result);

        Assert.Equal(input.Count('1'), (int)AsciiVector.CountMatches(vec));
    }

    [Fact]
    public static void Should_Return_PopCount()
    {
        Assert.Equal(CompressionTables.PopCount.Length, CompressionTables.PopCountMult2.Length);

        for (int i = 0; i < CompressionTables.PopCountMult2.Length; i++)
        {
            int popcnt = BitOperations.PopCount((uint)i);
            Assert.Equal(popcnt, CompressionTables.PopCount[i]);
            Assert.Equal(popcnt * 2, CompressionTables.PopCountMult2[i]);
        }
    }
}
