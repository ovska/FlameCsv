using System.Runtime.Intrinsics;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Tests;

public static class AsciiVectorTests
{
    [Fact]
    public static unsafe void Should_Narrow_256()
    {
        Assert.SkipUnless(Vector128.IsHardwareAccelerated, "Vector128 not supported");
        Assert.SkipUnless(BitConverter.IsLittleEndian, "Big-endian not supported");

        Span<char> data = stackalloc char[Vector256<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        fixed (char* ptr = data)
        {
            var vec = AsciiVector.Load256(ptr);
            Assert.All(vec.ToArray(), b => Assert.True(b > 127));

            data.Fill(',');
            vec = AsciiVector.Load256(ptr);
            Assert.Equal(new string(',', Vector256<byte>.Count), vec.ToAsciiString());
        }

        // spray & pray test
        byte[] bytes = new byte[Vector256<byte>.Count * sizeof(char)];
        byte[] buffer = new byte[Vector256<byte>.Count];
        List<string> invalid = [];

        fixed (byte* ptr = bytes)
        {
            for (int i = 0; i < 1024; i++)
            {
                Random.Shared.NextBytes(bytes);
                var vec = AsciiVector.Load256((char*)ptr);
                vec.CopyTo(buffer);

                for (int j = 0; j < buffer.Length; j++)
                {
                    if (buffer[j] != bytes[j * 2] && buffer[j] is < 127 and not 0)
                    {
                        invalid.Add($"[{j}]: {buffer[j]} != {bytes[j * 2]}");
                    }
                }

                if (invalid.Count > 0)
                {
                    Assert.Fail(
                        $"Invalid narrowing (iteration {i}): {Convert.ToHexStringLower(bytes)}\n{string.Join('\n', invalid)}"
                    );
                }
            }
        }
    }

    [Fact]
    public static unsafe void Should_Narrow_512()
    {
        Assert.SkipUnless(System.Runtime.Intrinsics.X86.Avx512F.IsSupported, "Vector512 not supported");
        Assert.SkipUnless(BitConverter.IsLittleEndian, "Big-endian not supported");

        Span<char> data = stackalloc char[Vector512<byte>.Count];
        data.Fill((char)(',' | (',' << 8)));

        fixed (char* ptr = data)
        {
            var vec = AsciiVector.Load512(ptr);
            Assert.All(vec.ToArray(), b => Assert.True(b > 127));

            data.Fill(',');
            vec = AsciiVector.Load512(ptr);
            Assert.Equal(new string(',', Vector512<byte>.Count), vec.ToAsciiString());
        }

        // spray & pray test
        byte[] bytes = new byte[Vector512<byte>.Count * sizeof(char)];
        byte[] buffer = new byte[Vector512<byte>.Count];
        List<string> invalid = [];

        fixed (byte* ptr = bytes)
        {
            for (int i = 0; i < 1024; i++)
            {
                Random.Shared.NextBytes(bytes);
                var vec = AsciiVector.Load512(ptr);
                vec.CopyTo(buffer);

                for (int j = 0; j < buffer.Length; j++)
                {
                    if (buffer[j] != bytes[j * 2] && buffer[j] < 127)
                    {
                        invalid.Add($"[{j}]: {buffer[j]} != {bytes[j * 2]}");
                    }
                }

                if (invalid.Count > 0)
                {
                    Assert.Fail(
                        $"Invalid narrowing (iteration {i}): {Convert.ToHexStringLower(bytes)}\n{string.Join('\n', invalid)}"
                    );
                }
            }
        }
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

    [Fact]
    public static void Should_Shift_First_Elements_256()
    {
        Span<byte> result = stackalloc byte[Vector256<byte>.Count];

        for (int i = 1; i < Vector256<byte>.Count; i++)
        {
            AsciiVector.ShiftItemsRight(Vector256<byte>.Indices, i).CopyTo(result);
            byte[] expected = Enumerable
                .Range(0, Vector256<byte>.Count)
                .Select(x => (byte)Math.Max(0, x - i))
                .ToArray();
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public static void Should_Shift_First_Elements_512()
    {
        Span<byte> result = stackalloc byte[Vector512<byte>.Count];

        for (int i = 1; i < Vector512<byte>.Count; i++)
        {
            AsciiVector.ShiftItemsRight(Vector512<byte>.Indices, i).CopyTo(result);
            byte[] expected = Enumerable
                .Range(0, Vector512<byte>.Count)
                .Select(x => (byte)Math.Max(0, x - i))
                .ToArray();
            Assert.Equal(expected, result);
        }
    }
}
