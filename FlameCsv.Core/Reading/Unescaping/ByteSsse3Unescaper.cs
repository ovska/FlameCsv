using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Unescaping;

/// <summary>
/// Unescapes bytes in 16 character blocks using SSSE3.
/// </summary>
internal readonly struct ByteSsse3Unescaper : ISimdUnescaper<byte, ushort, Vector128<byte>>
{
    public static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Ssse3.IsSupported && Vector128.IsHardwareAccelerated;
    }

    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128<byte>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> CreateVector(byte value) => Vector128.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<byte> LoadVector(ref readonly byte value, nuint offset = 0)
    {
        return Vector128.LoadUnsafe(in value, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreVector(Vector128<byte> vector, ref byte destination, nuint offset = 0)
    {
        vector.StoreUnsafe(ref destination, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort FindQuotes(Vector128<byte> value, Vector128<byte> quote)
    {
        return (ushort)Vector128.Equals(value, quote).ExtractMostSignificantBits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(Vector128<byte> data, ushort mask, ref byte destination, nuint offset = 0)
    {
        // Split the mask into two bytes (from least-significant to most-significant)
        byte mask1 = (byte)mask;
        byte mask2 = (byte)(mask >> 8);

        // Load the 64-bit values thintable_epi8[mask1] and thintable_epi8[mask2] into a 128-bit register
        Vector128<byte> shufmask = Vector128
            .Create(
                CompressionTables.ThinEpi8[mask1],
                CompressionTables.ThinEpi8[mask2] + 0x0808080808080808L)
            .AsByte();

        // Shuffle the source data
        Vector128<byte> pruned = Ssse3.Shuffle(data, shufmask);

        // Compute the popcount of the first word
        int pop1 = CompressionTables.PopCountMult2[mask1];

        // Load the corresponding mask
        Vector128<byte> compactmask = Vector128.LoadUnsafe(in CompressionTables.ShuffleCombine[0], (nuint)pop1 * 8);

        // Shuffle the pruned vector with the combined mask
        Vector128<byte> result = Ssse3.Shuffle(pruned, compactmask);
        result.StoreUnsafe(ref destination, offset);
    }
}
