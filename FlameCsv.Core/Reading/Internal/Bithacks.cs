using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Internal;

internal static class Bithacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nuint ComputeQuoteMask(nuint quoteBits)
    {
        if (Pclmulqdq.IsSupported && Sse2.X64.IsSupported && Vector128.IsHardwareAccelerated)
        {
            return (nuint)Sse2.X64.ConvertToUInt64(
                Pclmulqdq.CarrylessMultiply(
                    Vector128.Create(quoteBits, 0UL),
                    Vector128.Create((byte)0xFF).AsUInt64(),
                    0));
        }

        nuint mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        mask ^= (mask << 8);
        mask ^= (mask << 16);
        // ReSharper disable once ShiftExpressionRealShiftCountIsZero
        if (nuint.Size == 8) mask ^= (mask << 32);
        return mask;
    }

    /// <summary>
    /// Finds the quote mask for the current iteration, where bits between quotes are all 1's.
    /// </summary>
    /// <param name="quoteBits">Bitmask of the quote positions</param>
    /// <param name="prevIterInsideQuote">Whether the previous iteration ended in a quote</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nuint FindQuoteMask(nuint quoteBits, ref nuint prevIterInsideQuote)
    {
        nuint quoteMask = ComputeQuoteMask(quoteBits);

        // flip the bits that are inside quotes
        quoteMask ^= prevIterInsideQuote;

        // save if this iteration ended in a quote
        prevIterInsideQuote = (nuint)((long)(quoteMask) >> 63);

        return quoteMask;
    }
}
