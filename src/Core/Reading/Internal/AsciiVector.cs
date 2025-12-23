using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal static class AsciiVector
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static unsafe Vector256<byte> Load256<T>(T* source)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            // note: already does LDP under the hood on ARM64
            return Vector256.Load((byte*)source);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        if (Avx512BW.IsSupported)
        {
            Vector512<ushort> v = Vector512.Load((ushort*)source);
            return Avx512BW.ConvertToVector256ByteWithSaturation(v);
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            var (a0, a1) = AdvSimd.Arm64.LoadPairVector128((short*)source);
            Vector64<byte> lo01 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a0); // UQXTN
            Vector128<byte> out01 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo01, a1); // UQXTN2

            var (a2, a3) = AdvSimd.Arm64.LoadPairVector128((short*)source + (2 * Vector128<short>.Count));
            Vector64<byte> lo23 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a2); // UQXTN
            Vector128<byte> out23 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo23, a3); // UQXTN2

            return Vector256.Create(out01, out23);
        }

        Vector256<short> v0 = Vector256.Load((short*)source);
        Vector256<short> v1 = Vector256.Load((short*)source + (nuint)Vector256<short>.Count);

        if (Avx2.IsSupported)
        {
            // Avx2.PackUnsignedSaturate(Vector256.Create((short)1), Vector256.Create((short)2)) will result in
            // 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2
            // We want to swap the X and Y bits
            // 1, 1, 1, 1, 1, 1, 1, 1, X, X, X, X, X, X, X, X, Y, Y, Y, Y, Y, Y, Y, Y, 2, 2, 2, 2, 2, 2, 2, 2
            Vector256<byte> packed = Avx2.PackUnsignedSaturate(v0, v1);
            return Avx2.Permute4x64(packed.AsInt64(), 0b_11_01_10_00).AsByte();
        }

        Vector128<short> x0 = v0.GetLower();
        Vector128<short> x1 = v0.GetUpper();
        Vector128<short> y0 = v1.GetLower();
        Vector128<short> y1 = v1.GetUpper();

        if (Sse2.IsSupported)
        {
            Vector128<byte> lower = Sse2.PackUnsignedSaturate(x0, x1);
            Vector128<byte> upper = Sse2.PackUnsignedSaturate(y0, y1);
            return Vector256.Create(lower, upper);
        }

        if (PackedSimd.IsSupported)
        {
            Vector128<byte> lower = PackedSimd.ConvertNarrowingSaturateUnsigned(x0, x1);
            Vector128<byte> upper = PackedSimd.ConvertNarrowingSaturateUnsigned(y0, y1);
            return Vector256.Create(lower, upper);
        }

        if (AdvSimd.IsSupported)
        {
            Vector64<byte> lo01 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(x0); // UQXTN
            Vector128<byte> out01 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo01, x1); // UQXTN2

            Vector64<byte> lo23 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(y0); // UQXTN
            Vector128<byte> out23 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo23, y1); // UQXTN2

            return Vector256.Create(out01, out23);
        }

        throw new PlatformNotSupportedException("AsciiVector requires SIMD hardware support");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static unsafe Vector512<byte> Load512<T>(T* source)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            return Vector512.Load((byte*)source);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        Vector512<short> v0 = Vector512.Load((short*)source);
        Vector512<short> v1 = Vector512.Load((short*)source + (nuint)Vector512<short>.Count);
        Vector512<byte> packed = Avx512BW.PackUnsignedSaturate(v0, v1);
        return Avx512F.PermuteVar8x64(packed.AsInt64(), Vector512.Create(0, 2, 4, 6, 1, 3, 5, 7)).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static unsafe Vector512<byte> LoadAligned512<T>(T* ptr)
        where T : unmanaged, IBinaryInteger<T>
    {
        Check.Equal((nint)ptr % 64, 0, "Pointer must be 64-byte aligned");

        if (typeof(T) == typeof(byte))
        {
            return Vector512.LoadAligned((byte*)ptr);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        Vector512<short> v0 = Vector512.LoadAligned((short*)ptr);
        Vector512<short> v1 = Vector512.LoadAligned((short*)ptr + Vector512<short>.Count);
        Vector512<byte> packed = Avx512BW.PackUnsignedSaturate(v0, v1);
        return Avx512F.PermuteVar8x64(packed.AsInt64(), Vector512.Create(0L, 2, 4, 6, 1, 3, 5, 7)).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static uint MoveMask(this Vector128<byte> vec)
    {
        if (!AdvSimd.IsSupported)
            return vec.ExtractMostSignificantBits();

        Vector128<byte> w = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector128<sbyte> t0 = AdvSimd.And(vec, w).AsSByte();
        Vector128<sbyte> t1 = AdvSimd.And(vec, w).AsSByte();

        // vpadd across the two halves, then fold twice
        Vector128<sbyte> s = AdvSimd.Arm64.AddPairwise(t0, t1);
        s = AdvSimd.Arm64.AddPairwise(s, s);
        s = AdvSimd.Arm64.AddPairwise(s, s);

        // low 32 bits now hold the mask
        uint result = s.AsUInt16().ToScalar();
        Check.Equal(result, vec.ExtractMostSignificantBits());
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static uint MoveMask(this Vector256<byte> vec)
    {
        if (!AdvSimd.Arm64.IsSupported)
            return vec.ExtractMostSignificantBits();

        Vector128<byte> w = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector128<byte> lo = vec.GetLower();
        Vector128<byte> hi = vec.GetUpper();

        Vector128<sbyte> t0 = AdvSimd.And(lo, w).AsSByte();
        Vector128<sbyte> t1 = AdvSimd.And(hi, w).AsSByte();

        // vpadd across the two halves, then fold twice
        Vector128<sbyte> s = AdvSimd.Arm64.AddPairwise(t0, t1);
        s = AdvSimd.Arm64.AddPairwise(s, s);
        s = AdvSimd.Arm64.AddPairwise(s, s);

        // low 32 bits now hold the mask
        uint result = s.AsUInt32().ToScalar();
        Check.Equal(result, vec.ExtractMostSignificantBits());
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static (uint a, uint b, uint c, uint d) MoveMask<TCRLF>(
        Vector256<byte> v0,
        Vector256<byte> v1,
        Vector256<byte> v2,
        Vector256<byte> v3
    )
        where TCRLF : struct, IConstant
    {
        if (!AdvSimd.Arm64.IsSupported)
        {
            return (
                v0.ExtractMostSignificantBits(),
                v1.ExtractMostSignificantBits(),
                v2.ExtractMostSignificantBits(),
                TCRLF.Value ? v3.ExtractMostSignificantBits() : default
            );
        }

        Vector128<byte> w = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector128<byte> lo0 = v0.GetLower();
        Vector128<byte> hi0 = v0.GetUpper();
        Vector128<byte> lo1 = v1.GetLower();
        Vector128<byte> hi1 = v1.GetUpper();
        Vector128<byte> lo2 = v2.GetLower();
        Vector128<byte> hi2 = v2.GetUpper();
        Vector128<byte> lo3 = TCRLF.Value ? v3.GetLower() : default;
        Vector128<byte> hi3 = TCRLF.Value ? v3.GetUpper() : default;

        Vector128<sbyte> t0 = AdvSimd.And(lo0, w).AsSByte();
        Vector128<sbyte> t1 = AdvSimd.And(hi0, w).AsSByte();
        Vector128<sbyte> t2 = AdvSimd.And(lo1, w).AsSByte();
        Vector128<sbyte> t3 = AdvSimd.And(hi1, w).AsSByte();
        Vector128<sbyte> t4 = AdvSimd.And(lo2, w).AsSByte();
        Vector128<sbyte> t5 = AdvSimd.And(hi2, w).AsSByte();
        Vector128<sbyte> t6 = TCRLF.Value ? AdvSimd.And(lo3, w).AsSByte() : default;
        Vector128<sbyte> t7 = TCRLF.Value ? AdvSimd.And(hi3, w).AsSByte() : default;

        // vpadd across the two halves, then fold twice
        Vector128<sbyte> s0 = AdvSimd.Arm64.AddPairwise(t0, t1);
        Vector128<sbyte> s1 = AdvSimd.Arm64.AddPairwise(t2, t3);
        Vector128<sbyte> s2 = AdvSimd.Arm64.AddPairwise(t4, t5);
        Vector128<sbyte> s3 = TCRLF.Value ? AdvSimd.Arm64.AddPairwise(t6, t7) : default;

        s0 = AdvSimd.Arm64.AddPairwise(s0, s0);
        s1 = AdvSimd.Arm64.AddPairwise(s1, s1);
        s2 = AdvSimd.Arm64.AddPairwise(s2, s2);
        s3 = TCRLF.Value ? AdvSimd.Arm64.AddPairwise(s3, s3) : default;

        s0 = AdvSimd.Arm64.AddPairwise(s0, s0);
        s1 = AdvSimd.Arm64.AddPairwise(s1, s1);
        s2 = AdvSimd.Arm64.AddPairwise(s2, s2);
        s3 = TCRLF.Value ? AdvSimd.Arm64.AddPairwise(s3, s3) : default;

        uint r0 = s0.AsUInt32().ToScalar();
        uint r1 = s1.AsUInt32().ToScalar();
        uint r2 = s2.AsUInt32().ToScalar();
        uint r3 = TCRLF.Value ? s3.AsUInt32().ToScalar() : default;

        return (r0, r1, r2, r3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static ulong MoveMask(this Vector512<byte> vec)
    {
        if (!AdvSimd.Arm64.IsSupported)
            return vec.ExtractMostSignificantBits();

        Vector128<byte> weight = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector256<byte> lo = vec.GetLower();
        Vector256<byte> hi = vec.GetUpper();

        Vector128<byte> a = lo.GetLower();
        Vector128<byte> b = lo.GetUpper();
        Vector128<byte> c = hi.GetLower();
        Vector128<byte> d = hi.GetUpper();

        Vector128<byte> t0 = AdvSimd.And(a, weight);
        Vector128<byte> t1 = AdvSimd.And(b, weight);
        Vector128<byte> t2 = AdvSimd.And(c, weight);
        Vector128<byte> t3 = AdvSimd.And(d, weight);

        Vector128<byte> s0 = AdvSimd.Arm64.AddPairwise(t0, t1);
        Vector128<byte> s1 = AdvSimd.Arm64.AddPairwise(t2, t3);

        s0 = AdvSimd.Arm64.AddPairwise(s0, s1);
        s0 = AdvSimd.Arm64.AddPairwise(s0, s0);

        return s0.AsUInt64().ToScalar();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static uint MoveMask(this Vector512<int> vec)
    {
        if (!AdvSimd.Arm64.IsSupported)
            return (uint)vec.ExtractMostSignificantBits();

        // Extract 128-bit vectors
        Vector256<int> lo = vec.GetLower();
        Vector256<int> hi = vec.GetUpper();

        Vector128<int> a = lo.GetLower();
        Vector128<int> b = lo.GetUpper();
        Vector128<int> c = hi.GetLower();
        Vector128<int> d = hi.GetUpper();

        // Narrow int32 to int16, keeping MSBs
        Vector64<short> n0 = AdvSimd.ExtractNarrowingSaturateLower(a);
        Vector128<short> n1 = AdvSimd.ExtractNarrowingSaturateUpper(n0, b);
        Vector64<short> n2 = AdvSimd.ExtractNarrowingSaturateLower(c);
        Vector128<short> n3 = AdvSimd.ExtractNarrowingSaturateUpper(n2, d);

        // Narrow int16 to int8, keeping MSBs
        Vector64<sbyte> m0 = AdvSimd.ExtractNarrowingSaturateLower(n1);
        Vector128<sbyte> m1 = AdvSimd.ExtractNarrowingSaturateUpper(m0, n3);

        // Apply weight and pairwise add
        Vector128<byte> weight = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);
        Vector128<sbyte> t = AdvSimd.And(m1, weight.AsSByte());

        t = AdvSimd.Arm64.AddPairwise(t, t);
        t = AdvSimd.Arm64.AddPairwise(t, t);

        return t.AsUInt32().ToScalar();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> ShiftItemsRight(Vector256<byte> value, int amount)
    {
        Check.OverZero(amount);
        Check.LessThan(amount, Vector256<byte>.Count);

        if (Avx512Vbmi.VL.IsSupported)
        {
            Vector256<byte> indices = Vector256<byte>.Indices + Vector256.Create((byte)amount);
            return Avx512Vbmi.VL.PermuteVar32x8(value, indices);
        }

        Unsafe.SkipInit(out Inline64<byte> tmp);
        Vector256<byte>.Zero.StoreUnsafe(ref tmp.elem0); // zero init lower half
        value.StoreUnsafe(ref tmp.elem0, elementOffset: (nuint)amount);
        return Vector256.LoadUnsafe(ref tmp.elem0);
    }
}
