using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal static class AsciiVector
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Vector256<byte> Load256<T>(ref T source, nuint offset)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            return Vector256.LoadUnsafe(ref Unsafe.As<T, byte>(ref source), offset);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        if (Avx512BW.IsSupported)
        {
            Vector512<ushort> v = Vector512.LoadUnsafe(ref Unsafe.As<T, ushort>(ref source), offset);
            return Avx512BW.ConvertToVector256ByteWithSaturation(v);
        }

        Vector256<short> v0 = Vector256.LoadUnsafe(ref Unsafe.As<T, short>(ref source), offset);
        Vector256<short> v1 = Vector256.LoadUnsafe(
            ref Unsafe.As<T, short>(ref source),
            offset + (nuint)Vector256<short>.Count
        );

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

        throw new UnreachableException("AsciiVector requires SIMD hardware support");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Vector512<byte> Load512<T>(ref T source, nuint offset)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            return Vector512.LoadUnsafe(ref Unsafe.As<T, byte>(ref source), offset);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        Vector512<short> v0 = Vector512.LoadUnsafe(ref Unsafe.As<T, short>(ref source), offset);
        Vector512<short> v1 = Vector512.LoadUnsafe(
            ref Unsafe.As<T, short>(ref source),
            offset + (nuint)Vector512<short>.Count
        );

        return Avx512BW.PackUnsignedSaturate(v0, v1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static unsafe Vector512<byte> LoadAligned512<T>(void* ptr)
        where T : unmanaged, IBinaryInteger<T>
    {
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

        return Avx512BW.PackUnsignedSaturate(v0, v1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static uint MoveMask(this Vector256<byte> vec)
    {
        if (!AdvSimd.IsSupported)
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
        Debug.Assert(
            result == vec.ExtractMostSignificantBits(),
            $"MoveMask mismatch: {result:b32} vs {vec.ExtractMostSignificantBits():b32}"
        );
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static ulong MoveMask(Vector512<byte> vec)
    {
        if (!AdvSimd.IsSupported)
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
        if (!AdvSimd.IsSupported)
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
}
