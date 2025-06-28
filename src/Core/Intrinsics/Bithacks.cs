using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Intrinsics;

internal static class Bithacks
{
    /// <summary>
    /// Whether the current architecture prefers reversed bit manipulation (lzcnt instead of tzcnt).
    /// </summary>
    public static bool PreferReversed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64;
    }

    /// <summary>
    /// Checks if all bits in the mask are before the first bit set in the other value.
    /// </summary>
    /// <param name="mask">A non-zero bitmask</param>
    /// <param name="other">A non-zero bitmask</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllBitsBefore(nuint mask, nuint other)
    {
        Debug.Assert(mask > 0 && other > 0, "Both mask and other must be non-zero.");

        if (nuint.Size == 8)
        {
            return (63 ^ (int)BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
        }

        return (31 ^ (int)BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
    }

    /// <summary>
    /// Finds the quote mask for the current iteration, where bits between quotes are all 1's.
    /// </summary>
    /// <param name="quoteBits">Bitmask of the quote positions</param>
    /// <param name="prevIterInsideQuote">
    /// Whether the previous iteration ended in a quote; all bits 1 if ended in a quote; otherwise, zero
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T FindQuoteMask<T>(T quoteBits, ref T prevIterInsideQuote)
        where T : unmanaged, IBinaryInteger<T>
    {
        T quoteMask = ComputeQuoteMask(quoteBits);

        // flip the bits that are inside quotes
        quoteMask ^= prevIterInsideQuote;

        // save if this iteration ended in a quote
        prevIterInsideQuote = T.Zero - (quoteMask >> ((Unsafe.SizeOf<T>() * 8) - 1));

        return quoteMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMask<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Pclmulqdq.IsSupported && (Avx2.IsSupported || Sse2.IsSupported))
        {
            var vec = Vector128.CreateScalar(ulong.CreateTruncating(quoteBits));
            var result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
            return T.CreateTruncating(result.GetElement(0));
        }

        // no separate PMULL path, see:
        // https://github.com/simdjson/simdjson/blob/d84c93476894dc3230e7379cd9322360435dd0f9/include/simdjson/arm64/bitmask.h#L24

        // Fallback to software implementation
        T mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        if (Unsafe.SizeOf<T>() >= sizeof(ushort))
            mask ^= (mask << 8);
        if (Unsafe.SizeOf<T>() >= sizeof(uint))
            mask ^= (mask << 16);
        if (Unsafe.SizeOf<T>() >= sizeof(ulong))
            mask ^= (mask << 32);
        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMaskSoftwareFallback<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        T mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        if (Unsafe.SizeOf<T>() >= sizeof(ushort))
            mask ^= (mask << 8);
        if (Unsafe.SizeOf<T>() >= sizeof(uint))
            mask ^= (mask << 16);
        if (Unsafe.SizeOf<T>() >= sizeof(ulong))
            mask ^= (mask << 32);
        return mask;
    }

    internal static int StorePositions(uint mask, int index, ref int destination)
    {
        // should be rare and predictable
        if (mask == 0)
        {
            return 0;
        }

        int count = BitOperations.PopCount((uint)mask);

        Unsafe.Add(ref destination, index + 0) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 1) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 2) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 3) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 4) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 5) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 6) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref destination, index + 7) = BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);

        if (count > 8)
        {
            Unsafe.Add(ref destination, index + 8) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 9) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 10) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 11) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 12) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 13) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 14) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            Unsafe.Add(ref destination, index + 15) = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
        }
        if (count > 16)
        {
            index += 16;

            do
            {
                Unsafe.Add(ref destination, index) = BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                index++;
            } while (mask != 0);
        }

        return count;
    }
}
