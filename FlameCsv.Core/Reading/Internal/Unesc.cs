using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

internal static class Unesc
{
    const int UInt32Bits = 8 * sizeof(uint);

    internal static void FillBitmask(char quote, uint quoteCount, ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length < UInt32Bits)
        {
            Sequential(quote, (int)quoteCount, source, destination);
            return;
        }

        // fill the bits in destination with source.Length bytes, each bit set if the corresponding character is a quote
        // the quote bits are shifted finally to find pairs of quotes, e.g.:
        //  James ""007"" Bond
        //  000000110001100000 &
        // 000000110001100000 =
        // 000000010000100000

        // 1 extra bit for the shift
        nuint maskCount = (nuint)(source.Length + (UInt32Bits - 1)) / UInt32Bits;
        Span<uint> masks = stackalloc uint[(int)maskCount];
        int offset = FillBitmask<char, Vec256Char>(quote, (int)quoteCount, source, masks);

        ref char src = ref MemoryMarshal.GetReference(source);
        ref char dst = ref MemoryMarshal.GetReference(destination);
        ref uint maskRef = ref MemoryMarshal.GetReference(masks);

        nint srcIndex = 0; // index of examined characters in the source
        nint dstIndex = 0; // count of copied characters in the destination
        nint pendingCopyStart = 0; // index of the first uncopied character

        // Process first mask with special offset handling
        ProcessMask(
            ref src,
            ref dst,
            ref srcIndex,
            ref dstIndex,
            ref pendingCopyStart,
            maskRef >> (UInt32Bits - offset), // shift by the difference
            maskConsumed: (UInt32Bits + UInt32Bits - offset) % UInt32Bits); // pass bits untouched if offset is 0

        // Process remaining masks if any
        nuint maskPos = 1;

        while (maskPos < maskCount)
        {
            ProcessMask(
                ref src,
                ref dst,
                ref srcIndex,
                ref dstIndex,
                ref pendingCopyStart,
                Unsafe.Add(ref maskRef, maskPos),
                0);
            maskPos++;
        }

        // Copy any remaining pending data at the end
        if (srcIndex > pendingCopyStart)
        {
            Copy(ref src, pendingCopyStart, ref dst, dstIndex, srcIndex - pendingCopyStart);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMask(
        ref char src,
        ref char dst,
        ref nint srcIndex,
        ref nint dstIndex,
        ref nint pendingCopyStart,
        uint mask,
        int maskConsumed)
    {
        while (mask != 0)
        {
            // Find position of next quote
            int quoteOffset = BitOperations.TrailingZeroCount(mask) - maskConsumed;
            nint quotePosition = srcIndex + quoteOffset;

            nint length = quotePosition - pendingCopyStart;
            Copy(ref src, pendingCopyStart, ref dst, dstIndex, length);
            dstIndex += length;

            // Skip the quote character
            srcIndex = quotePosition + 1;
            maskConsumed += quoteOffset + 1;
            pendingCopyStart = srcIndex; // the next copy will start after this quote

            // Clear the processed quote bit
            mask &= mask - 1;
        }

        // Update srcIndex for remaining characters but don't copy them yet
        srcIndex += UInt32Bits - maskConsumed;
    }

    /*
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void ProcessMask(
    ref char src,
    ref char dst,
    ref nint srcIndex,
    ref nint dstIndex,
    ref nint pendingCopyStart,
    uint mask,
    int maskConsumed)
{
    // Process quotes in the mask
    while (mask != 0)
    {
        // Find position of next quote
        int quoteOffset = BitOperations.TrailingZeroCount(mask) - maskConsumed;
        nint quotePosition = srcIndex + quoteOffset;

        // Copy characters from pendingCopyStart up to but not including the quote
        if (quotePosition > pendingCopyStart)
        {
            nint length = quotePosition - pendingCopyStart;
            Copy(ref src, pendingCopyStart, ref dst, dstIndex, length);
            dstIndex += length;
        }

        // Skip the quote character
        srcIndex = quotePosition + 1;
        pendingCopyStart = srcIndex; // Next copy will start after this quote
        maskConsumed += quoteOffset + 1;

        // Clear the processed quote bit
        mask &= mask - 1;
    }

    // Update srcIndex for remaining characters but don't copy them yet
    srcIndex += UInt32Bits - maskConsumed;
}
*/

    private static void Sequential<T>(T quote, int quoteCount, ReadOnlySpan<T> source, ReadOnlySpan<T> destination)
        where T : unmanaged, IBinaryInteger<T>
    {
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nint srcRemaining = source.Length;
        nint srcIndex = 0;
        nint dstIndex = 0;

        while (srcRemaining > 1)
        {
            if (Unsafe.Add(ref src, srcIndex) == quote)
            {
                if (Unsafe.Add(ref src, srcIndex + 1) != quote) Invalid(source);
                quoteCount -= 2;
                srcIndex++;
                srcRemaining--;
            }

            Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
            srcIndex++;
            dstIndex++;
            srcRemaining--;
        }

        if (quoteCount != 0) Invalid(source);
        if (srcRemaining == 1) Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
    }

    private static int FillBitmask<T, TVector>(
        T quote,
        int quoteCount,
        ReadOnlySpan<T> source,
        Span<uint> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<uint>
    {
        Debug.Assert(source.Length >= TVector.Count); // source must fit at least one vector
        Debug.Assert(source.Length >= sizeof(uint)); // source must fit at least one mask
        Debug.Assert(UInt32Bits * destination.Length >= source.Length); // all bits of the source must fit
        Debug.Assert(BitOperations.IsPow2(TVector.Count)); // vector size must be a power of 2
        Debug.Assert(!destination.ContainsAnyExcept(0u)); // destination must be zeroed
        Debug.Assert(quoteCount % 2 == 0); // quotes must be in pairs

        TVector quoteVec = TVector.Create(quote);

        ref T src = ref MemoryMarshal.GetReference(source);
        ref uint mask = ref MemoryMarshal.GetReference(destination);

        int offsetFromEnd = source.Length & (TVector.Count - 1);
        nuint srcPos = 0;
        nuint maskPos = 0;
        nint remaining = source.Length;
        uint carry = 0;

        // Process first vector with special offset handling
        ProcessVector(
            ref quoteCount,
            ref src,
            ref mask,
            ref srcPos,
            ref maskPos,
            ref remaining,
            ref carry,
            quoteVec,
            offsetFromEnd,
            true);

        // Process remaining vectors
        while (remaining > 0 && quoteCount > 0)
        {
            ProcessVector(
                ref quoteCount,
                ref src,
                ref mask,
                ref srcPos,
                ref maskPos,
                ref remaining,
                ref carry,
                quoteVec,
                TVector.Count,
                false);
        }

        if (quoteCount != 0) Invalid(source);
        return offsetFromEnd;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessVector<T, TVector>(
        ref int quoteCount,
        ref T src,
        ref uint mask,
        ref nuint srcPos,
        ref nuint maskPos,
        ref nint remaining,
        ref uint carry,
        TVector quoteVec,
        int advanceBy,
        bool isFirstVector)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<uint>
    {
        TVector current = TVector.LoadUnaligned(ref src, srcPos);
        TVector eq = TVector.Equals(current, quoteVec);

        uint bits = eq.MoveMask();

        if (isFirstVector)
        {
            bits <<= (TVector.Count - advanceBy);
        }

        uint shifted = bits << 1 | carry;
        Unsafe.Add(ref mask, maskPos) = bits & shifted;

        quoteCount -= BitOperations.PopCount(bits);
        carry = bits >> 31;
        srcPos += (nuint)advanceBy;
        remaining -= advanceBy;
        maskPos++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Copy<T>(ref T src, nint srcIndex, ref T dst, nint dstIndex, nint length) where T : unmanaged
    {
        Unsafe.CopyBlockUnaligned(
            Unsafe.AsPointer(ref Unsafe.Add(ref dst, dstIndex)),
            Unsafe.AsPointer(ref Unsafe.Add(ref src, srcIndex)),
            (uint)length * (uint)Unsafe.SizeOf<T>());
    }

    [DoesNotReturn]
    static void Invalid<T>(ReadOnlySpan<T> input) => throw new InvalidOperationException(input.ToString());
}
