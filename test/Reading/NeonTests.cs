using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
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

        byte[] bytes = new byte[Vector256<byte>.Count];

        foreach (var _ in Enumerable.Range(0, 1000))
        {
            Random.Shared.NextBytes(bytes);

            Vector256<byte> vec = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));

            string expected = vec.ExtractMostSignificantBits().ToString("b32");
            string actual = vec.MoveMask().ToString("b32");

            Assert.Equal(expected, actual);
        }
    }

    [Theory, MemberData(nameof(SignBits))]
    public static void Should_Load_Int_Sign_Bits_To_Masks(int[] data)
    {
        byte[] expected = data.Select(i => (byte)(i < 0 ? 0xFF : 0x00)).ToArray();
        Vector256<byte> result = AsciiVector.LoadInt32SignsToByteMasksARM(ref Unsafe.As<int, uint>(ref data[0]), 0);
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
