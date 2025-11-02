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
            Vector128<byte> lower = Vector128.Create(
                AdvSimd.ExtractNarrowingSaturateUnsignedLower(x0),
                AdvSimd.ExtractNarrowingSaturateUnsignedLower(x1)
            );

            Vector128<byte> upper = Vector128.Create(
                AdvSimd.ExtractNarrowingSaturateUnsignedLower(y0),
                AdvSimd.ExtractNarrowingSaturateUnsignedLower(y1)
            );

            return Vector256.Create(lower, upper);
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<ushort> max = Vector256.Create((ushort)127);
            Vector256<ushort> lower = Vector256.Min(v0.AsUInt16(), max);
            Vector256<ushort> upper = Vector256.Min(v1.AsUInt16(), max);
            return Vector256.Narrow(lower, upper);
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<ushort> max = Vector128.Create((ushort)127);
            Vector128<byte> lower = Vector128.Narrow(
                Vector128.Min(x0.AsUInt16(), max),
                Vector128.Min(x1.AsUInt16(), max)
            );
            Vector128<byte> upper = Vector128.Narrow(
                Vector128.Min(y0.AsUInt16(), max),
                Vector128.Min(y1.AsUInt16(), max)
            );

            return Vector256.Create(lower, upper);
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

        if (Avx512BW.IsSupported)
        {
            Vector512<short> v0 = Vector512.LoadUnsafe(ref Unsafe.As<T, short>(ref source), offset);
            Vector512<short> v1 = Vector512.LoadUnsafe(
                ref Unsafe.As<T, short>(ref source),
                offset + (nuint)Vector512<short>.Count
            );

            return Avx512BW.PackUnsignedSaturate(v0, v1);
        }

        if (AdvSimd.IsSupported)
        {
            ref short s0 = ref Unsafe.Add(ref Unsafe.As<T, short>(ref source), offset);

            Vector128<short> a0 = Vector128.LoadUnsafe(ref s0);
            Vector128<short> a1 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count);
            Vector128<short> a2 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 2);
            Vector128<short> a3 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 3);
            Vector128<short> a4 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 4);
            Vector128<short> a5 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 5);
            Vector128<short> a6 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 6);
            Vector128<short> a7 = Vector128.LoadUnsafe(ref s0, (nuint)Vector128<short>.Count * 7);

            Vector64<byte> b0 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a0);
            Vector64<byte> b1 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a1);
            Vector64<byte> b2 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a2);
            Vector64<byte> b3 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a3);
            Vector64<byte> b4 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a4);
            Vector64<byte> b5 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a5);
            Vector64<byte> b6 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a6);
            Vector64<byte> b7 = AdvSimd.ExtractNarrowingSaturateUnsignedLower(a7);

            Vector128<byte> c0 = Vector128.Create(b0, b1);
            Vector128<byte> c1 = Vector128.Create(b2, b3);
            Vector128<byte> c2 = Vector128.Create(b4, b5);
            Vector128<byte> c3 = Vector128.Create(b6, b7);

            return Vector512.Create(Vector256.Create(c0, c1), Vector256.Create(c2, c3));
        }

        throw new UnreachableException("AsciiVector requires SIMD hardware support");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static unsafe Vector512<byte> LoadAligned512<T>(ref T source, nuint offset)
        where T : unmanaged, IBinaryInteger<T>
    {
        void* ptr = Unsafe.AsPointer(ref Unsafe.Add(ref source, offset));

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
    public static ulong MoveMaskARM64(Vector512<byte> vector)
    {
        Vector128<byte> weight = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);

        Vector256<byte> lower = vector.GetLower();
        Vector256<byte> upper = vector.GetUpper();

        Vector128<byte> a = lower.GetLower();
        Vector128<byte> b = lower.GetUpper();
        Vector128<byte> c = upper.GetLower();
        Vector128<byte> d = upper.GetUpper();

        Vector128<byte> t0 = AdvSimd.And(a, weight);
        Vector128<byte> t1 = AdvSimd.And(b, weight);
        Vector128<byte> t2 = AdvSimd.And(c, weight);
        Vector128<byte> t3 = AdvSimd.And(d, weight);

        Vector128<byte> s0 = AdvSimd.Arm64.AddPairwise(t0.AsSByte(), t1.AsSByte()).AsByte();
        Vector128<byte> s1 = AdvSimd.Arm64.AddPairwise(t2.AsSByte(), t3.AsSByte()).AsByte();

        s0 = AdvSimd.Arm64.AddPairwise(s0.AsSByte(), s1.AsSByte()).AsByte();
        s0 = AdvSimd.Arm64.AddPairwise(s0.AsSByte(), s0.AsSByte()).AsByte();

        return s0.AsUInt64().ToScalar();
    }

    extension(Vector512)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Equals128(Vector512<byte> left, Vector128<byte> right)
        {
            Vector256<byte> lower = left.GetLower();
            Vector256<byte> upper = left.GetUpper();
            Vector128<byte> a = lower.GetLower();
            Vector128<byte> b = lower.GetUpper();
            Vector128<byte> c = upper.GetLower();
            Vector128<byte> d = upper.GetUpper();

            Vector128<byte> eqA = Vector128.Equals(a, right);
            Vector128<byte> eqB = Vector128.Equals(b, right);
            Vector128<byte> eqC = Vector128.Equals(c, right);
            Vector128<byte> eqD = Vector128.Equals(d, right);

            return Vector512.Create(Vector256.Create(eqA, eqB), Vector256.Create(eqC, eqD));
        }
    }
}
