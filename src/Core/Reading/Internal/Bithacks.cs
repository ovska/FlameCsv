using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Internal;

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
}
