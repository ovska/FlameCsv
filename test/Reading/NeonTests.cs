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
    public static unsafe void Should_Load_Vector()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        fixed (char* charPtr = data)
        fixed (byte* bytePtr = dataBytes)
        {
            Vector256<byte> vecBytes = AsciiVector.Load256(bytePtr);
            Vector256<byte> vecChars = AsciiVector.Load256(charPtr);

            for (int i = 0; i < Vector256<byte>.Count; i++)
            {
                byte expected = (byte)data[i];
                Assert.Equal(expected, vecBytes.GetElement(i));
                Assert.Equal(expected, vecChars.GetElement(i));
            }
        }
    }

    [Fact]
    public static unsafe void Should_Narrow_Correctly()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        Span<char> data = stackalloc char[Vector256<byte>.Count];

        fixed (char* ptr = data)
        {
            for (int i = 0; i < 1024; i++)
            {
                data.Fill((char)i);
                Vector256<byte> vec = AsciiVector.Load256(ptr);
                Assert.Equal(Math.Min(i, byte.MaxValue), vec.GetElement(0));
            }
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

            Vector128<byte> vec128 = Vector128.GreaterThan(Vector128.Create((byte)127), Vector128.Create(bytes, 0));
            string expected = vec128.ExtractMostSignificantBits().ToString("b16");
            string actual = vec128.MoveMask().ToString("b16");
            Assert.Equal(expected, actual);

            Vector256<byte> vec256 = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            expected = vec256.ExtractMostSignificantBits().ToString("b32");
            actual = vec256.MoveMask().ToString("b32");
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
}
