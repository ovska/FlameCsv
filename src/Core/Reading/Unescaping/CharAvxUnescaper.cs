#if SIMD_UNESCAPING
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Unescaping;

/// <summary>
/// Unescapes characters in 16 (two 8) character blocks using AVX and SSSE3.
/// </summary>
internal readonly struct CharAvxUnescaper : ISimdUnescaper<char, ushort, Vector256<short>>
{
    public static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx.IsSupported && Ssse3.IsSupported && Vector256.IsHardwareAccelerated;
    }

    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<short>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> CreateVector(char value) => Vector256.Create((short)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> LoadVector(ref readonly char value, nuint offset = 0)
    {
        return Vector256.LoadUnsafe(ref Unsafe.As<char, short>(ref Unsafe.AsRef(in value)), offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreVector(Vector256<short> vector, ref char destination, nuint offset = 0)
    {
        vector.StoreUnsafe(ref Unsafe.As<char, short>(ref destination), offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort FindQuotes(Vector256<short> value, Vector256<short> quote)
    {
        return (ushort)Vector256.Equals(value, quote).ExtractMostSignificantBits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(Vector256<short> value, ushort mask, ref char destination, nuint offset = 0)
    {
        byte mask1 = (byte)mask;
        byte mask2 = (byte)(mask >> 8);

        Vector128<byte> shuffleMask1 = Vector128.LoadUnsafe(in CompressionTables.Mask128Epi16[mask1 * 16]);
        Vector128<byte> shuffleMask2 = Vector128.LoadUnsafe(in CompressionTables.Mask128Epi16[mask2 * 16]);

        var lower = Ssse3.Shuffle(value.GetLower().AsByte(), shuffleMask1).AsInt16();
        var upper = Ssse3.Shuffle(value.GetUpper().AsByte(), shuffleMask2).AsInt16();

        // Calculate the offset for the upper half.
        int countOnes = BitOperations.PopCount(mask1);

        // Store the results.
        ref short dst = ref Unsafe.As<char, short>(ref destination);
        lower.StoreUnsafe(ref dst, offset);
        upper.StoreUnsafe(ref dst, offset + (nuint)Vector128<short>.Count - (nuint)countOnes);
    }
}
#endif
