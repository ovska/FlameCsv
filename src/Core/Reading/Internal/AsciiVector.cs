using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

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

        ref short src = ref Unsafe.As<T, short>(ref Unsafe.Add(ref source, offset));
        Vector256<short> v0 = Vector256.LoadUnsafe(ref src);
        Vector256<short> v1 = Vector256.LoadUnsafe(ref src, (nuint)Vector256<short>.Count);

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

        if (AdvSimd.IsSupported)
        {
            ref short src = ref Unsafe.As<T, short>(ref Unsafe.Add(ref source, offset));
            int width = Vector128<short>.Count;

            Vector128<short> v0 = Vector128.LoadUnsafe(ref src, 0 * (nuint)width);
            Vector128<short> v1 = Vector128.LoadUnsafe(ref src, 1 * (nuint)width);
            Vector128<short> v2 = Vector128.LoadUnsafe(ref src, 2 * (nuint)width);
            Vector128<short> v3 = Vector128.LoadUnsafe(ref src, 3 * (nuint)width);
            Vector128<short> v4 = Vector128.LoadUnsafe(ref src, 4 * (nuint)width);
            Vector128<short> v5 = Vector128.LoadUnsafe(ref src, 5 * (nuint)width);
            Vector128<short> v6 = Vector128.LoadUnsafe(ref src, 6 * (nuint)width);
            Vector128<short> v7 = Vector128.LoadUnsafe(ref src, 7 * (nuint)width);

            Vector64<byte> lo01 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(v0); // UQXTN
            Vector128<byte> out01 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo01, v1); // UQXTN2

            Vector64<byte> lo23 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(v2); // UQXTN
            Vector128<byte> out23 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo23, v3); // UQXTN2

            Vector64<byte> lo45 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(v4); // UQXTN
            Vector128<byte> out45 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo45, v5); // UQXTN2

            Vector64<byte> lo67 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(v6); // UQXTN
            Vector128<byte> out67 = AdvSimd.ExtractNarrowingSaturateUnsignedUpper(lo67, v7); // UQXTN2

            return Vector512.Create(Vector256.Create(out01, out23), Vector256.Create(out45, out67));
        }
        else
        {
            Vector512<short> v0 = Vector512.LoadUnsafe(ref Unsafe.As<T, short>(ref source), offset);
            Vector512<short> v1 = Vector512.LoadUnsafe(
                ref Unsafe.As<T, short>(ref source),
                offset + (nuint)Vector512<short>.Count
            );

            Vector512<byte> packed = Avx512BW.PackUnsignedSaturate(v0, v1);
            return Avx512F.PermuteVar8x64(packed.AsInt64(), Vector512.Create(0L, 2, 4, 6, 1, 3, 5, 7)).AsByte();
        }
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

        Vector512<byte> packed = Avx512BW.PackUnsignedSaturate(v0, v1);
        return Avx512F.PermuteVar8x64(packed.AsInt64(), Vector512.Create(0L, 2, 4, 6, 1, 3, 5, 7)).AsByte();
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
    public static (uint a, uint b, uint c) MoveMask(Vector256<byte> v0, Vector256<byte> v1, Vector256<byte> v2)
    {
        if (!AdvSimd.IsSupported)
            return (v0.ExtractMostSignificantBits(), v1.ExtractMostSignificantBits(), v2.ExtractMostSignificantBits());

        Vector128<byte> w = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector128<byte> lo0 = v0.GetLower();
        Vector128<byte> hi0 = v0.GetUpper();
        Vector128<byte> lo1 = v1.GetLower();
        Vector128<byte> hi1 = v1.GetUpper();
        Vector128<byte> lo2 = v2.GetLower();
        Vector128<byte> hi2 = v2.GetUpper();

        Vector128<sbyte> t0 = AdvSimd.And(lo0, w).AsSByte();
        Vector128<sbyte> t1 = AdvSimd.And(hi0, w).AsSByte();
        Vector128<sbyte> t2 = AdvSimd.And(lo1, w).AsSByte();
        Vector128<sbyte> t3 = AdvSimd.And(hi1, w).AsSByte();
        Vector128<sbyte> t4 = AdvSimd.And(lo2, w).AsSByte();
        Vector128<sbyte> t5 = AdvSimd.And(hi2, w).AsSByte();

        // vpadd across the two halves, then fold twice
        Vector128<sbyte> s0 = AdvSimd.Arm64.AddPairwise(t0, t1);
        Vector128<sbyte> s1 = AdvSimd.Arm64.AddPairwise(t2, t3);
        Vector128<sbyte> s2 = AdvSimd.Arm64.AddPairwise(t4, t5);

        s0 = AdvSimd.Arm64.AddPairwise(s0, s0);
        s1 = AdvSimd.Arm64.AddPairwise(s1, s1);
        s2 = AdvSimd.Arm64.AddPairwise(s2, s2);

        s0 = AdvSimd.Arm64.AddPairwise(s0, s0);
        s1 = AdvSimd.Arm64.AddPairwise(s1, s1);
        s2 = AdvSimd.Arm64.AddPairwise(s2, s2);

        uint r0 = s0.AsUInt32().ToScalar();
        uint r1 = s1.AsUInt32().ToScalar();
        uint r2 = s2.AsUInt32().ToScalar();

        return (r0, r1, r2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static ulong MoveMask(this Vector512<byte> vec)
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

    /// <summary>
    /// Loads 32 ints, narrowing them to bytes on ARM64. EOL fields are all 0xFF, others are 0x00.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> LoadInt32SignsToByteMasksARM(ref int field, nuint pos)
    {
        // don't change this to uint - the offset arithmetic would be wrong

        if (!AdvSimd.IsSupported)
            throw new UnreachableException();

        // jagged order to improve instruction-level parallelism

        // load even
        Vector128<int> a0 = Vector128.LoadUnsafe(ref field, pos + (0 * (nuint)Vector128<int>.Count));
        Vector128<int> a2 = Vector128.LoadUnsafe(ref field, pos + (2 * (nuint)Vector128<int>.Count));
        Vector128<int> a4 = Vector128.LoadUnsafe(ref field, pos + (4 * (nuint)Vector128<int>.Count));
        Vector128<int> a6 = Vector128.LoadUnsafe(ref field, pos + (6 * (nuint)Vector128<int>.Count));

        // load odd
        Vector128<int> a1 = Vector128.LoadUnsafe(ref field, pos + (1 * (nuint)Vector128<int>.Count));
        Vector128<int> a3 = Vector128.LoadUnsafe(ref field, pos + (3 * (nuint)Vector128<int>.Count));
        Vector128<int> a5 = Vector128.LoadUnsafe(ref field, pos + (5 * (nuint)Vector128<int>.Count));
        Vector128<int> a7 = Vector128.LoadUnsafe(ref field, pos + (7 * (nuint)Vector128<int>.Count));

        // narrow even
        Vector64<short> b0 = AdvSimd.ExtractNarrowingSaturateLower(a0);
        Vector64<short> b2 = AdvSimd.ExtractNarrowingSaturateLower(a2);
        Vector64<short> b4 = AdvSimd.ExtractNarrowingSaturateLower(a4);
        Vector64<short> b6 = AdvSimd.ExtractNarrowingSaturateLower(a6);

        // narrow odd
        Vector128<short> c0 = AdvSimd.ExtractNarrowingSaturateUpper(b0, a1);
        Vector128<short> c2 = AdvSimd.ExtractNarrowingSaturateUpper(b4, a5);
        Vector128<short> c1 = AdvSimd.ExtractNarrowingSaturateUpper(b2, a3);
        Vector128<short> c3 = AdvSimd.ExtractNarrowingSaturateUpper(b6, a7);

        // narrow even
        Vector64<sbyte> d0 = AdvSimd.ExtractNarrowingSaturateLower(c0);
        Vector64<sbyte> d1 = AdvSimd.ExtractNarrowingSaturateLower(c2);

        // narrow odd
        Vector128<sbyte> e0 = AdvSimd.ExtractNarrowingSaturateUpper(d0, c1);
        Vector128<sbyte> e1 = AdvSimd.ExtractNarrowingSaturateUpper(d1, c3);

        // convert to 0xFF or 0x00 (required by movemask emulation)
        Vector128<byte> r0 = AdvSimd.ShiftRightArithmetic(e0, 7).AsByte();
        Vector128<byte> r1 = AdvSimd.ShiftRightArithmetic(e1, 7).AsByte();

        return Vector256.Create(r0, r1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ZeroOrOneMatches(Vector256<byte> vector)
    {
        // values are either 0x00 or 0xFF
        Vector64<ushort> lower = AdvSimd.Arm64.AddAcrossWidening(vector.GetLower());
        Vector64<ushort> upper = AdvSimd.Arm64.AddAcrossWidening(vector.GetUpper());
        return (lower + upper).ToScalar() <= 0xFF;
    }

    extension(Vector256)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static Vector256<byte> Equals128(Vector256<byte> vector, Vector128<byte> value)
        {
            var x0 = vector.GetLower();
            var x1 = vector.GetUpper();
            return Vector256.Create(Vector128.Equals(x0, value), Vector128.Equals(x1, value));
        }
    }
}
