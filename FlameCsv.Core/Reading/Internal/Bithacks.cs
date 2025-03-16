using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FlameCsv.Reading.Internal;

internal static class Bithacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMask<T>(T quoteBits) where T : unmanaged, IBinaryInteger<T>
    {
        if (Pclmulqdq.IsSupported && Sse2.X64.IsSupported && Vector128.IsHardwareAccelerated)
        {
            ulong result = Sse2.X64.ConvertToUInt64(
                Pclmulqdq.CarrylessMultiply(
                    Vector128.Create(ulong.CreateTruncating(quoteBits), 0UL),
                    Vector128.Create((byte)0xFF).AsUInt64(),
                    0));

            return T.CreateTruncating(result);
        }

        T mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        mask ^= (mask << 8);
        mask ^= (mask << 16);
        mask ^= (mask << 32);
        return mask;
    }

    /// <summary>
    /// Finds the quote mask for the current iteration, where bits between quotes are all 1's.
    /// </summary>
    /// <param name="quoteBits">Bitmask of the quote positions</param>
    /// <param name="prevIterInsideQuote">Whether the previous iteration ended in a quote</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T FindQuoteMask<T>(T quoteBits, ref T prevIterInsideQuote) where T : unmanaged, IBinaryInteger<T>
    {
        T quoteMask = ComputeQuoteMask(quoteBits);

        // flip the bits that are inside quotes
        quoteMask ^= prevIterInsideQuote;

        // save if this iteration ended in a quote
        prevIterInsideQuote = quoteMask >> (Unsafe.SizeOf<T>() * 8 - 1);

        return quoteMask;
    }
}
