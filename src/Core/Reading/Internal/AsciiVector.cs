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

    extension(Vector256<byte> vec)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public uint MoveMask()
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
            return s.AsUInt32().ToScalar();
        }
    }
}
