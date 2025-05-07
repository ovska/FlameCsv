#if SIMD_UNESCAPING
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Unescaping;

/// <summary>
/// Unescapes bytes in 32 character blocks using AVX2.
/// </summary>
internal readonly struct ByteAvx2Unescaper : ISimdUnescaper<byte, uint, Vector256<byte>>
{
    public static bool IsSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Avx2.IsSupported && Vector256.IsHardwareAccelerated;
    }

    public static int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> CreateVector(byte value) => Vector256.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<byte> LoadVector(ref readonly byte value, nuint offset = 0)
    {
        return Vector256.LoadUnsafe(in value, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StoreVector(Vector256<byte> vector, ref byte destination, nuint offset = 0)
    {
        vector.StoreUnsafe(ref destination, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FindQuotes(Vector256<byte> value, Vector256<byte> quote)
    {
        return Vector256.Equals(value, quote).ExtractMostSignificantBits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Compress(Vector256<byte> value, uint mask, ref byte destination, nuint offset = 0)
    {
        // Split the mask into four bytes
        byte mask1 = (byte)mask;
        byte mask2 = (byte)(mask >> 8);
        byte mask3 = (byte)(mask >> 16);
        byte mask4 = (byte)(mask >> 24);

        // Build the 256-bit shuffle mask.
        Vector256<byte> shufmask = Vector256
            .Create(
                CompressionTables.ThinEpi8[mask1],
                CompressionTables.ThinEpi8[mask2],
                CompressionTables.ThinEpi8[mask3],
                CompressionTables.ThinEpi8[mask4])
            .AsByte();

        // Create a constant to add to the shuffle mask.
        // When interpreted as 32 bytes in little-endian order, this constant is:
        // [ 0,0,0,0,... 8,8,8,8,... 16,16,16,16,...  24,24,24,24,... ]
        // We build it here using 8 ints (element 0 is the lowest).
        Vector256<byte> addConst = Vector256
            .Create(
                0,
                0,
                0x08080808,
                0x08080808,
                0x10101010,
                0x10101010,
                0x18181818,
                0x18181818
            )
            .AsByte();

        // Add the constant to the shuffle mask.
        Vector256<byte> shufmaskBytes = Vector256.Add(shufmask, addConst);

        // Shuffle the source data.
        Vector256<byte> pruned = Avx2.Shuffle(value, shufmaskBytes);

        // Use precomputed popcounts from the table.
        int pop1 = CompressionTables.PopCountMult2[mask1];
        int pop3 = CompressionTables.PopCountMult2[mask3];

        // Load the 128-bit masks from pshufb_combine_table.
        Vector128<byte> combine0 = Vector128.LoadUnsafe(in CompressionTables.ShuffleCombine[0], (nuint)pop1 * 8);
        Vector128<byte> combine1 = Vector128.LoadUnsafe(in CompressionTables.ShuffleCombine[0], (nuint)pop3 * 8);

        // Combine the two 128-bit lanes into a 256-bit mask.
        Vector256<byte> compactmask = Vector256.Create(combine0, combine1);

        // Shuffle the pruned vector with the combined mask.
        Vector256<byte> almostthere = Avx2.Shuffle(pruned, compactmask);

        // Extract the lower and upper 128-bit lanes.
        Vector128<byte> lower = almostthere.GetLower();
        Vector128<byte> upper = almostthere.GetUpper();

        // Calculate the offset for the upper half.
        int countOnes = BitOperations.PopCount(mask & 0xFFFF);

        // Store the results.
        lower.StoreUnsafe(ref destination, offset);
        upper.StoreUnsafe(ref destination, offset + (nuint)Vector128<byte>.Count - (nuint)countOnes);
    }
}
#endif
