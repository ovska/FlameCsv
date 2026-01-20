using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Extensions;

namespace FlameCsv.Tests;

public static class SmokeTests
{
    [Fact]
    public static void Test_Avx2()
    {
        Assert.SkipUnless(Avx2.IsSupported, "AVX2 not supported");
        var v0 = Vector256.Create((ushort)0xFFFF).AsInt16();
        var v1 = Vector256.Create((ushort)0xFFFF).AsInt16();
        Vector256<byte> result = Avx2.PackUnsignedSaturate(v0, v1);
        result = Avx2.Permute4x64(result.AsInt64(), 0b_11_01_10_00).AsByte();
        Assert.All(result.ToArray(), static b => Assert.True(b is 0 or 0xFF));
    }

    [Fact]
    public static void Test_Avx512()
    {
        Assert.SkipUnless(Avx512BW.IsSupported, "AVX512BW not supported");
        Vector512<short> v0 = Vector512.Create((ushort)0x7FFF).AsInt16();
        Vector512<short> v1 = Vector512.Create((ushort)0xFFFF).AsInt16();
        Vector512<byte> result = Avx512BW.PackUnsignedSaturate(v0, v1);
        result = Avx512F.PermuteVar8x64(result.AsInt64(), Vector512.Create(0, 2, 4, 6, 1, 3, 5, 7)).AsByte();
        Assert.All(result.ToArray(), static b => Assert.True(b is 0 or 0xFF));
    }

    [Fact]
    public static void Test_Neon()
    {
        Assert.SkipUnless(AdvSimd.Arm64.IsSupported, "ARM64 not supported");
        Vector128<ushort> v0 = Vector128.Create((ushort)0xFFFF);
        Vector128<ushort> v1 = Vector128.Create((ushort)0xFFFF);
        Vector64<byte> lo01 = AdvSimd.ExtractNarrowingSaturateLower(v0); // UQXTN
        Vector128<byte> out01 = AdvSimd.ExtractNarrowingSaturateUpper(lo01, v1); // UQXTN2
        Assert.All(out01.ToArray(), static b => Assert.True(b is 0 or 0xFF));
    }
}
