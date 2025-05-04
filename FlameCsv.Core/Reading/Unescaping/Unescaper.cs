using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Unescaping;

internal static class Unescaper
{
    /// <summary>
    /// Returns the minimum buffer length required to unescape.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBufferLength<TUnescaper>(int length)
        where TUnescaper : ISimdUnescaper, allows ref struct
    {
        return ((length + TUnescaper.Count - 1) / TUnescaper.Count) * TUnescaper.Count;
    }

    /// <summary>
    /// Finds pairs of ones in an unsigned integer bitmask with a size of 32 bits or fewer.
    /// </summary>
    public static TMask ShiftMask32<TMask>(TMask mask)
        where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
    {
        if (Unsafe.SizeOf<TMask>() is sizeof(uint) or sizeof(ushort))
        {
            ulong result = ((ulong.CreateTruncating(mask) * 0xAAAAAAABUL) >> 33);
            return TMask.CreateTruncating(result);
        }

        throw new NotSupportedException("Mask must be 32 or 16 bytes: " + typeof(TMask).Name);
    }

    public static int Unescape<T, TMask, TVector, TUnescaper>(T quote, ReadOnlySpan<T> source, Span<T> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
        where TVector : struct
        where TUnescaper : struct, ISimdUnescaper<T, TMask, TVector>
    {
        // destination should fit at least one vector
        Debug.Assert(destination.Length >= TUnescaper.Count);
        Debug.Assert(destination.Length >= GetBufferLength<TUnescaper>(source.Length));

        TVector quoteVector = TUnescaper.CreateVector(quote);
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint srcOffset = 0;
        nuint dstOffset = 0;
        nint remaining = source.Length;
        TMask carry = TMask.Zero;

        int remainder = source.Length & (TUnescaper.Count - 1);
        int firstLength = remainder | ((remainder - 1) >> TUnescaper.Count & TUnescaper.Count);
        int padding = TUnescaper.Count - firstLength;

        TVector data = TUnescaper.LoadVector(in src);
        TMask mask = TUnescaper.FindQuotes(data, quoteVector);

        // shift first mask left to discard values after the length
        mask <<= padding;
        TMask oddIndex = FindOddBackslashSequences(mask, ref carry);

        if (oddIndex != TMask.Zero) goto Invalid;

        // shift the mask back so we work from the beginning of the value
        mask >>= padding;

        // Fix for the edge case: if padding is 1 and carry is 1, add back the carry bit
        // This prevents ShiftMask32 from seeing an unpaired quote
        mask |= ((carry & TMask.CreateTruncating(padding) & TMask.One) << (TUnescaper.Count - 1));

        // merge the bits of each quote pair
        mask = ShiftMask32(mask);
        uint quotePairs = uint.CreateTruncating(TMask.PopCount(mask));

        // write the result
        if (mask == TMask.Zero)
        {
            TUnescaper.StoreVector(data, ref dst);
        }
        else
        {
            TUnescaper.Compress(data, mask, ref dst);
        }

        srcOffset += (nuint)firstLength;
        dstOffset += (nuint)firstLength - quotePairs;
        remaining -= firstLength;

        while (remaining > 0)
        {
            data = TUnescaper.LoadVector(in src, srcOffset);
            mask = TUnescaper.FindQuotes(data, quoteVector);
            oddIndex = FindOddBackslashSequences(mask, ref carry);

            if (oddIndex != TMask.Zero) goto Invalid;

            // merge the bits of each quote pair
            mask = ShiftMask32(mask);
            quotePairs = uint.CreateTruncating(TMask.PopCount(mask));

            if (mask == TMask.Zero)
            {
                TUnescaper.StoreVector(data, ref dst, dstOffset);
            }
            else
            {
                // write the result
                TUnescaper.Compress(data, mask, ref dst, dstOffset);
            }

            srcOffset += (nuint)TUnescaper.Count;
            dstOffset += (nuint)TUnescaper.Count - quotePairs;
            remaining -= TUnescaper.Count;
        }

        if (carry != TMask.Zero) goto Invalid;

        return (int)dstOffset;

        Invalid:
        Throw(source, srcOffset, oddIndex, carry);
        return 0;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw<T, TMask>(ReadOnlySpan<T> source, nuint offset, TMask oddIndex, TMask carry)
        where T : unmanaged, IBinaryInteger<T>
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        string value = source.AsPrintableString();

        if (carry == TMask.Zero)
        {
            throw new CsvFormatException($"Field had an unpaired quote at the end: {value}");
        }

        throw new CsvFormatException(
            $"Field had an unpaired quote at index {(int)offset + long.CreateTruncating(TMask.PopCount(oddIndex))}: {value}");
    }

    #region Bithacks

    // Source: SimdJson library
    // (c) Daniel Lemire and Geoff Langdale

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TMask FindOddBackslashSequences<TMask>(TMask bsBits, ref TMask prevIterEndsOddBackslash)
        where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
    {
        TMask evenBits = TMask.CreateTruncating(0x5555_5555_5555_5555UL);
        TMask oddBits = TMask.CreateTruncating(0xAAAA_AAAA_AAAA_AAAAUL);

        TMask startEdges = bsBits & ~(bsBits << 1);
        TMask evenStartMask = evenBits ^ prevIterEndsOddBackslash;
        TMask evenStarts = startEdges & evenStartMask;
        TMask oddStarts = startEdges & ~evenStartMask;
        TMask evenCarries = bsBits + evenStarts;
        bool iterEndsOddBackslash = AddCarry(bsBits, oddStarts, out TMask oddCarries);
        oddCarries |= prevIterEndsOddBackslash;
        prevIterEndsOddBackslash = iterEndsOddBackslash ? TMask.One : TMask.Zero;
        TMask evenCarryEnds = evenCarries & ~bsBits;
        TMask oddCarryEnds = oddCarries & ~bsBits;
        TMask evenStartOddEnd = evenCarryEnds & oddBits;
        TMask oddStartEvenEnd = oddCarryEnds & evenBits;
        TMask oddEnds = evenStartOddEnd | oddStartEvenEnd;
        return oddEnds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AddCarry<TMask>(TMask value1, TMask value2, out TMask result)
        where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
    {
        unchecked
        {
            result = value1 + value2;
            return result < value1;
        }
    }

    #endregion Bithacks
}
