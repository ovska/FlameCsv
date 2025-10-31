using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
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

        ulong mask = AsciiVector.MoveMaskARM64(eq);

        Assert.Equal(0b10001000100010001000100010001000UL, mask);
    }
}
