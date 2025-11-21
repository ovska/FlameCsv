#if false
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

internal static class SWAR
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong LoadUnsafe<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(char))
        {
            return Unsafe.ReadUnaligned<ulong>(in Unsafe.As<T, byte>(ref value));
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint GetLength<T>()
        where T : unmanaged, IBinaryInteger<T>
    {
        return sizeof(ulong) / Unsafe.SizeOf<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Ones<T>()
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => 0x01_01_01_01_01_01_01_01UL,
            sizeof(char) => 0x0001_0001_0001_0001UL,
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Highs<T>()
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => 0x80_80_80_80_80_80_80_80UL,
            sizeof(char) => 0x8000_8000_8000_8000UL,
            _ => throw Token<T>.NotSupported,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ComputeMask<T>(ulong hay, ulong mask)
        where T : unmanaged, IBinaryInteger<T>
    {
        ulong x0 = hay ^ mask;
        return (x0 - Ones<T>()) & ~x0 & Highs<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Create<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            ulong v = Unsafe.BitCast<T, byte>(value);
            return v * Ones<T>();
        }

        if (typeof(T) == typeof(char))
        {
            ulong v = Unsafe.BitCast<T, char>(value);
            return v * Ones<T>();
        }
        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount<T>(ulong value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(byte) => BitOperations.TrailingZeroCount(value) >> 3,
            sizeof(char) => BitOperations.TrailingZeroCount(value) >> 4,
            _ => throw Token<T>.NotSupported,
        };
    }
}
#endif
