using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using CommunityToolkit.HighPerformance;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public static class NeonTests
{
    const string data = "abc,def,ghi,jkl,mno,pqr,stu,vwx,yz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data);

    [Fact]
    public static void Should_Load_Vector()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        Vector256<byte> vecBytes = AsciiVector.Load256(ref dataBytes[0], 0);
        Vector256<byte> vecChars = AsciiVector.Load256(ref MemoryMarshal.GetReference(data.AsSpan()), 0);

        for (int i = 0; i < Vector256<byte>.Count; i++)
        {
            byte expected = (byte)data[i];
            Assert.Equal(expected, vecBytes.GetElement(i));
            Assert.Equal(expected, vecChars.GetElement(i));
        }
    }

    [Fact]
    public static void Should_Narrow_Correctly()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        Span<char> data = stackalloc char[Vector256<byte>.Count];

        for (int i = 0; i < 1024; i++)
        {
            data.Fill((char)i);
            Vector256<byte> vec = AsciiVector.Load256(ref data[0], 0);
            Assert.Equal(Math.Min(i, byte.MaxValue), vec.GetElement(0));
        }
    }

    [Fact]
    public static void Should_MoveMask()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        byte[] bytes = new byte[Vector512<byte>.Count];

        for (int i = 0; i < 1024; i++)
        {
            Random.Shared.NextBytes(bytes);

            Vector256<byte> vec256 = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            string expected = vec256.ExtractMostSignificantBits().ToString("b32");
            string actual = vec256.MoveMask().ToString("b32");
            Assert.Equal(expected, actual);

            Vector512<byte> vec512 = Vector512.GreaterThan(Vector512.Create((byte)127), Vector512.Create(bytes, 0));
            expected = vec512.ExtractMostSignificantBits().ToString("b64");
            actual = vec512.MoveMask().ToString("b64");
            Assert.Equal(expected, actual);

            // test trifecta
            Vector256<byte> a = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> b = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> c = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> d = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));

            var (x, y, z, w) = AsciiVector.MoveMask<TrueConstant>(a, b, c, d);
            string expectedX = a.ExtractMostSignificantBits().ToString("b32");
            string expectedY = b.ExtractMostSignificantBits().ToString("b32");
            string expectedZ = c.ExtractMostSignificantBits().ToString("b32");
            string expectedW = d.ExtractMostSignificantBits().ToString("b32");
            Assert.Equal(expectedX, a.MoveMask().ToString("b32"));
            Assert.Equal(expectedY, b.MoveMask().ToString("b32"));
            Assert.Equal(expectedZ, c.MoveMask().ToString("b32"));
            Assert.Equal(expectedW, d.MoveMask().ToString("b32"));
            Assert.Equal(expectedX, x.ToString("b32"));
            Assert.Equal(expectedY, y.ToString("b32"));
            Assert.Equal(expectedZ, z.ToString("b32"));
            Assert.Equal(expectedW, w.ToString("b32"));
        }
    }

    [Theory, MemberData(nameof(SignBits))]
    public static void Should_Load_Int_Sign_Bits_To_Masks(int[] data)
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        byte[] expected = data.Select(i => (byte)(i < 0 ? 0xFF : 0x00)).ToArray();
        Vector256<byte> result = AsciiVector.LoadInt32SignsToByteMasksARM(ref data[0], 0);
        byte[] actual = new byte[32];
        result.CopyTo(actual);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<int[]> SignBits()
    {
        return new()
        {
            new int[32],
            Enumerable.Range(0, 32).Select(i => i % 2 == 0 ? -1 : 0).ToArray(),
            Enumerable.Range(0, 32).Select(i => i % 5 == 0 ? -1 : 0).ToArray(),
            Enumerable.Range(0, 32).Select(i => i % 7 == 0 ? -1 : 0).ToArray(),
            Enumerable.Range(0, 32).Select(i => i % 11 == 0 ? -1 : 0).ToArray(),
            Enumerable.Range(0, 32).Select(_ => -1).ToArray(),
        };
    }
}
