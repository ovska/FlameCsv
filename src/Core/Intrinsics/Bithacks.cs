using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Intrinsics;

internal static class Bithacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetMaskUpToLowestSetBit(uint mask)
    {
        return mask ^ (mask - 1); // lowered to blsmsk on x86
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSubractionFlag<TNewline>(uint carriageReturn)
        where TNewline : struct, INewline
    {
        // for CRLF sequences, subtracting the flag shifts offset left by 1 (as the bits are done on LF positions)
        // and sets the top 2 bits
        // for LFs, only the MSB (eol bit) is set when subtracting
        return TNewline.IsCRLF && carriageReturn != 0 ? 0x40000001u : Field.IsEOL;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ProcessFlag(uint maskNewline, uint tz, uint flag)
    {
        uint newlineBit = (maskNewline >> (int)tz) & 1;
        return (uint)(-(int)newlineBit & flag);
    }

    /// <summary>
    /// Checks if all bits in the mask are before the first bit set in the other value.
    /// </summary>
    /// <param name="mask">A non-zero bitmask</param>
    /// <param name="other">A non-zero bitmask</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllBitsBefore(nuint mask, nuint other)
    {
        Debug.Assert(mask > 0 && other > 0, $"Both mask and other must be non-zero, were: {mask:B} and {other:B}");

        // this is a slight optimization over using BitOperations.Log2 directly, as it must ensure that mask is not zero
        if (nuint.Size == 8)
        {
            return (63 ^ BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
        }

        return (31 ^ BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
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

    /// <inheritdoc cref="FindQuoteMask{T}(T, ref T)"/>
    internal static ulong FindQuoteMask(ulong quoteBits, ref ulong prevIterInsideQuote)
    {
        Vector128<ulong> vec = Vector128.CreateScalar(quoteBits);
        Vector128<ulong> result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
        ulong quoteMask = result.GetElement(0);
        quoteMask ^= prevIterInsideQuote;
        prevIterInsideQuote = 0UL - (quoteMask >> 63);
        return quoteMask;
    }

    /// <summary>
    /// Flips all bits in <paramref name="value"/> is condition is odd.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConditionalFlipQuotes<T>(ref T value, T condition)
        where T : unmanaged, IBinaryInteger<T>
    {
        T mask = T.Zero - (condition & T.One); // Extract LSB and create all-1s or all-0s mask
        value ^= mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ZeroOrOneBitsSet<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return (value & (value - T.One)) == T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMask<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Pclmulqdq.IsSupported && Sse2.IsSupported)
        {
            var vec = Vector128.CreateScalar(ulong.CreateTruncating(quoteBits));
            var result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
            return T.CreateTruncating(result.GetElement(0));
        }

        // no separate PMULL path, see:
        // https://github.com/simdjson/simdjson/blob/d84c93476894dc3230e7379cd9322360435dd0f9/include/simdjson/arm64/bitmask.h#L24

        return ComputeQuoteMaskSoftwareFallback(quoteBits);
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
