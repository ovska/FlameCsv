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

        Vector512<byte> vecBytes512 = AsciiVector.Load512(ref dataBytes[0], 0);
        Vector512<byte> vecChars512 = AsciiVector.Load512(ref MemoryMarshal.GetReference(data.AsSpan()), 0);

        for (int i = 0; i < Vector512<byte>.Count; i++)
        {
            byte expected = (byte)data[i];
            Assert.Equal(expected, vecBytes512.GetElement(i));
            Assert.Equal(expected, vecChars512.GetElement(i));
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_MoveMask_On_Arm64(bool bytes)
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");

        Vector512<byte> vec = bytes
            ? AsciiVector.Load512(ref dataBytes[0], 0)
            : AsciiVector.Load512(ref MemoryMarshal.GetReference(data.AsSpan()), 0);
        Vector512<byte> eq = Vector512.Equals(vec, Vector512.Create((byte)','));

        ulong mask = AsciiVector.Arm.MoveMask(eq);

        Assert.Equal(0b10001000100010001000100010001000UL, mask);
    }

    [Fact]
    public static void Should_Shift_Right()
    {
        Vector512<byte> vec = Vector512.Create(dataBytes, 0);
        (Vector512<byte> shifted, Vector128<byte> carry) = AsciiVector.Arm.ShiftAndCarry(vec, Vector128<byte>.Zero);

        byte[] result = ToArray(shifted);
        byte[] expected = [0, .. dataBytes[..(Vector512<byte>.Count - 1)]];
        Assert.Equal(expected, result);
        Assert.Equal(dataBytes[Vector512<byte>.Count - 1], carry.GetElement(15));

        // ensure carry brings over the last item from the previous block
        Vector512<byte> vec2 = Vector512.Create((byte)'^');
        (Vector512<byte> shifted2, carry) = AsciiVector.Arm.ShiftAndCarry(vec2, carry);
        byte[] result2 = ToArray(shifted2);
        byte[] expected2 =
        [
            dataBytes[Vector512<byte>.Count - 1],
            .. Enumerable.Repeat((byte)'^', Vector512<byte>.Count - 1),
        ];
        Assert.Equal(expected2, result2);
    }

    [Fact]
    public static void Should_Count_Matches()
    {
        Assert.Equal(0u, AsciiVector.Arm.CountNonZero(Vector512<byte>.Zero));
        Assert.Equal(64u, AsciiVector.Arm.CountNonZero(Vector512<byte>.AllBitsSet));

        byte[] dat = new byte[Vector512<byte>.Count];
        dat[7] = 0xFF;
        dat[15] = 0xFF;
        dat[49] = 0xFF;

        Assert.Equal(3u, AsciiVector.Arm.CountNonZero(Vector512.Create(dat, 0)));
    }

    [Theory]
    [InlineData("0", "0", false)]
    [InlineData("0", "1", false)]
    [InlineData("1", "1", false)]
    [InlineData("000100", "000100", false)]
    [InlineData("000000", "000100", false)]
    [InlineData("000100", "010100", true)]
    [InlineData("010000", "010100", true)]
    [InlineData("010100", "010000", true)]
    [InlineData("010100", "000100", true)]
    [InlineData("000100", "000000", true)]
    public static void Should_Check_If_Disjoint_CR(string cr, string lf, bool expected)
    {
        var crVec = ToVec(cr);
        var lfVec = ToVec(lf);
        bool result = AsciiVector.Arm.IsDisjointCR(lfVec, crVec);
        Assert.Equal(expected, result);

        static Vector512<byte> ToVec(string value)
        {
            byte[] bytes = value.PadLeft(64, '0').Select(c => c == '1' ? (byte)0xFF : (byte)0).ToArray();
            return Vector512.Create(bytes, 0);
        }
    }

    [Fact]
    public static void Should_Check_If_Zero()
    {
        Assert.True(AsciiVector.Arm.IsZero(Vector512<byte>.Zero));
        Assert.False(AsciiVector.Arm.IsZero(Vector512<byte>.Indices));
        Assert.False(AsciiVector.Arm.IsZero(Vector512<byte>.AllBitsSet));
    }

    private static byte[] ToArray(Vector512<byte> vec)
    {
        byte[] arr = new byte[Vector512<byte>.Count];
        vec.CopyTo(arr);
        return arr;
    }
}
