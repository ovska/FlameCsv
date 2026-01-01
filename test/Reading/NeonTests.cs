using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
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
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "Not AArch");

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
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "Not AArch");

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
            Vector256<byte> vecControl = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> vecLF = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> vecQuote = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));
            Random.Shared.NextBytes(bytes);
            Vector256<byte> vecCR = Vector256.GreaterThan(Vector256.Create((byte)127), Vector256.Create(bytes, 0));

            var (maskControl, maskLF, maskQuote, maskCR) = AsciiVector.MoveMask<TrueConstant, TrueConstant>(
                vecControl,
                vecLF,
                vecQuote,
                vecCR
            );
            string expectedCtrl = vecControl.ExtractMostSignificantBits().ToString("b32");
            string expectedLF = vecLF.ExtractMostSignificantBits().ToString("b32");
            string expectedQuot = vecQuote.ExtractMostSignificantBits().ToString("b32");
            string expectedCR = vecCR.ExtractMostSignificantBits().ToString("b32");
            Assert.Equal(expectedCtrl, vecControl.MoveMask().ToString("b32"));
            Assert.Equal(expectedLF, vecLF.MoveMask().ToString("b32"));
            Assert.Equal(expectedQuot, vecQuote.MoveMask().ToString("b32"));
            Assert.Equal(expectedCR, vecCR.MoveMask().ToString("b32"));
            Assert.Equal(expectedCtrl, maskControl.ToString("b32"));
            Assert.Equal(expectedLF, maskLF.ToString("b32"));
            Assert.Equal(expectedQuot, maskQuote.ToString("b32"));
            Assert.Equal(expectedCR, maskCR.ToString("b32"));

            // test without crlf
            (maskControl, maskLF, maskQuote, _) = AsciiVector.MoveMask<FalseConstant, TrueConstant>(
                vecControl,
                vecLF,
                vecQuote,
                vecCR
            );
            expectedCtrl = vecControl.ExtractMostSignificantBits().ToString("b32");
            expectedLF = vecLF.ExtractMostSignificantBits().ToString("b32");
            expectedQuot = vecQuote.ExtractMostSignificantBits().ToString("b32");
            Assert.Equal(expectedCtrl, vecControl.MoveMask().ToString("b32"));
            Assert.Equal(expectedLF, vecLF.MoveMask().ToString("b32"));
            Assert.Equal(expectedQuot, vecQuote.MoveMask().ToString("b32"));
            Assert.Equal(expectedCtrl, maskControl.ToString("b32"));
            Assert.Equal(expectedLF, maskLF.ToString("b32"));
            Assert.Equal(expectedQuot, maskQuote.ToString("b32"));

            // test without quote
            (maskControl, maskLF, _, maskCR) = AsciiVector.MoveMask<TrueConstant, FalseConstant>(
                vecControl,
                vecLF,
                vecQuote,
                vecCR
            );
            expectedCtrl = vecControl.ExtractMostSignificantBits().ToString("b32");
            expectedLF = vecLF.ExtractMostSignificantBits().ToString("b32");
            expectedCR = vecCR.ExtractMostSignificantBits().ToString("b32");
            Assert.Equal(expectedCtrl, vecControl.MoveMask().ToString("b32"));
            Assert.Equal(expectedLF, vecLF.MoveMask().ToString("b32"));
            Assert.Equal(expectedCR, vecCR.MoveMask().ToString("b32"));
            Assert.Equal(expectedCtrl, maskControl.ToString("b32"));
            Assert.Equal(expectedLF, maskLF.ToString("b32"));
            Assert.Equal(expectedCR, maskCR.ToString("b32"));

            // test without either
            (maskControl, maskLF, _, _) = AsciiVector.MoveMask<FalseConstant, FalseConstant>(
                vecControl,
                vecLF,
                vecQuote,
                vecCR
            );
            expectedCtrl = vecControl.ExtractMostSignificantBits().ToString("b32");
            expectedLF = vecLF.ExtractMostSignificantBits().ToString("b32");
            Assert.Equal(expectedCtrl, vecControl.MoveMask().ToString("b32"));
            Assert.Equal(expectedLF, vecLF.MoveMask().ToString("b32"));
            Assert.Equal(expectedCtrl, maskControl.ToString("b32"));
            Assert.Equal(expectedLF, maskLF.ToString("b32"));
        }
    }
}
