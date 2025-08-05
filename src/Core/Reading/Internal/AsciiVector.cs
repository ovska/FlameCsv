using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Internal;

internal static class AsciiVector
{
    public const int Count = 32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Vector256<byte> Create<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Vector256.Create(Unsafe.BitCast<T, byte>(value)),
            sizeof(char) => Vector256.Create((byte)Unsafe.BitCast<T, ushort>(value)),
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Vector512<byte> Create512<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => Vector512.Create(Unsafe.BitCast<T, byte>(value)),
            sizeof(char) => Vector512.Create((byte)Unsafe.BitCast<T, ushort>(value)),
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static Vector256<byte> Load<T>(ref T source, nuint offset)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            // automagically loads two 128bit vectors if 256-bit vectors are not supported
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

        if (Avx512BW.VL.IsSupported)
        {
            return Vector256.Create(
                Avx512BW.VL.ConvertToVector128ByteWithSaturation(v0.AsUInt16()),
                Avx512BW.VL.ConvertToVector128ByteWithSaturation(v1.AsUInt16())
            );
        }

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
        else if (Vector128.IsHardwareAccelerated)
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
            // automagically loads two 128bit vectors if 256-bit vectors are not supported
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
    public static unsafe Vector512<byte> LoadAligned512<T>(ref T source, nuint offset)
        where T : unmanaged, IBinaryInteger<T>
    {
        ref byte b = ref Unsafe.As<T, byte>(ref Unsafe.Add(ref source, (nint)offset));

        if (typeof(T) == typeof(byte))
        {
            // automagically loads two 128bit vectors if 256-bit vectors are not supported
            return Vector512.LoadAligned((byte*)Unsafe.AsPointer(ref b));
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        Vector512<short> v0 = Vector512.LoadAligned((short*)Unsafe.AsPointer(ref b));
        Vector512<short> v1 = Vector512.LoadAligned((short*)Unsafe.AsPointer(ref b) + Vector512<short>.Count);

        return Avx512BW.PackUnsignedSaturate(v0, v1);
    }
}
