using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

internal static class Unesc
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Exec<T>(T quote, int quoteCount, ReadOnlySpan<T> source, Span<T> destination)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(source.Length >= 2);

        if (typeof(T) == typeof(char))
        {
            char quoteChar = Unsafe.As<T, char>(ref quote);
            ReadOnlySpan<char> sourceChars = MemoryMarshal.Cast<T, char>(source);
            Span<char> destinationChars = MemoryMarshal.Cast<T, char>(destination);

            if (Vec256Char.IsSupported && source.Length >= Vec256Char.Count)
            {
                FillBitmask<char, Vec256Char, uint>(quoteChar, quoteCount, sourceChars, destinationChars);
                return;
            }

            if (Vec128Char.IsSupported && source.Length >= Vec128Char.Count)
            {
                FillBitmask<char, Vec128Char, ushort>(quoteChar, quoteCount, sourceChars, destinationChars);
                return;
            }

            if (Vec64Char.IsSupported && source.Length >= Vec64Char.Count)
            {
                FillBitmask<char, Vec64Char, byte>(quoteChar, quoteCount, sourceChars, destinationChars);
                return;
            }
        }

        if (typeof(T) == typeof(byte))
        {
            byte quoteByte = Unsafe.As<T, byte>(ref quote);
            ReadOnlySpan<byte> sourceBytes = MemoryMarshal.Cast<T, byte>(source);
            Span<byte> destinationBytes = MemoryMarshal.Cast<T, byte>(destination);

            if (Vec256Byte.IsSupported && source.Length >= Vec256Byte.Count)
            {
                FillBitmask<byte, Vec256Byte, uint>(quoteByte, quoteCount, sourceBytes, destinationBytes);
                return;
            }

            if (Vec128Byte.IsSupported && source.Length >= Vec128Byte.Count)
            {
                FillBitmask<byte, Vec128Byte, ushort>(quoteByte, quoteCount, sourceBytes, destinationBytes);
                return;
            }

            if (Vec64Byte.IsSupported && source.Length >= Vec64Byte.Count)
            {
                FillBitmask<byte, Vec64Byte, byte>(quoteByte, quoteCount, sourceBytes, destinationBytes);
                return;
            }
        }

        Sequential(quote, quoteCount, source, destination);
    }

    internal static void FillBitmask<T, TVector, TMask>(
        T quote,
        int quoteCount,
        ReadOnlySpan<T> source,
        Span<T> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<TMask>
        where TMask : unmanaged, IUnsignedNumber<TMask>, IBinaryInteger<TMask>
    {
        Debug.Assert(Unsafe.SizeOf<TMask>() * 8 == TVector.Count);
        Debug.Assert(source.Length >= TVector.Count);

        // fill the bits in destination with source.Length bytes, each bit set if the corresponding character is a quote
        // the quote bits are shifted finally to find pairs of quotes, e.g.:
        //  James ""007"" Bond
        //  000000110001100000 &
        // 000000110001100000 =
        // 000000010000100000

        // 1 extra bit for the shift
        nuint maskCount = (nuint)(source.Length + (TVector.Count - 1)) / (nuint)TVector.Count;
        Span<TMask> masks = stackalloc TMask[(int)maskCount];
        int firstLength = FillMaskArray<T, TVector, TMask>(quote, quoteCount, source, masks);

        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        ref TMask maskRef = ref MemoryMarshal.GetReference(masks);

        nint srcIndex = 0; // index of examined characters in the source
        nint dstIndex = 0; // count of copied characters in the destination
        nint pendingCopyStart = 0; // index of the first uncopied character

        // Process first mask with special offset handling
        ProcessMask<T, TVector, TMask>(
            ref src,
            ref dst,
            ref srcIndex,
            ref dstIndex,
            ref pendingCopyStart,
            maskRef >> (firstLength & (TVector.Count - 1)), // shift by the difference
            maskConsumed: TVector.Count - firstLength);

        // Process remaining masks if any
        nuint maskPos = 1;

        while (maskPos < maskCount)
        {
            ProcessMask<T, TVector, TMask>(
                ref src,
                ref dst,
                ref srcIndex,
                ref dstIndex,
                ref pendingCopyStart,
                Unsafe.Add(ref maskRef, maskPos),
                maskConsumed: 0);
            maskPos++;
        }

        // Copy any remaining pending data at the end
        if (srcIndex > pendingCopyStart)
        {
            Copy(ref src, pendingCopyStart, ref dst, dstIndex, srcIndex - pendingCopyStart);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMask<T, TVector, TMask>(
        ref T src,
        ref T dst,
        ref nint srcIndex,
        ref nint dstIndex,
        ref nint pendingCopyStart,
        TMask mask,
        int maskConsumed)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<TMask>
        where TMask : unmanaged, IUnsignedNumber<TMask>, IBinaryInteger<TMask>
    {
        while (mask != TMask.Zero)
        {
            // Find position of next quote
            int quoteOffset = int.CreateTruncating(TMask.TrailingZeroCount(mask)) - maskConsumed;
            // int quoteOffset = Math.Max(0, int.CreateTruncating(TMask.TrailingZeroCount(mask)) - maskConsumed);
            nint quotePosition = srcIndex + quoteOffset;

            nint length = quotePosition - pendingCopyStart;
            Copy(ref src, pendingCopyStart, ref dst, dstIndex, length);
            dstIndex += length;

            // Skip the quote character
            srcIndex = quotePosition + 1;
            maskConsumed += quoteOffset + 1;
            pendingCopyStart = srcIndex; // the next copy will start after this quote

            // Clear the processed quote bit
            mask &= mask - TMask.One;
        }

        // Update srcIndex for remaining characters but don't copy them yet
        srcIndex += TVector.Count - maskConsumed;
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
                if (Unsafe.Add(ref src, srcIndex + 1) != quote) Invalid(source, Reason.NonConsecutive);
                quoteCount -= 2;
                srcIndex++;
                srcRemaining--;
            }

            Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
            srcIndex++;
            dstIndex++;
            srcRemaining--;
        }

        if (quoteCount != 0) Invalid(source, Reason.QuoteCount);
        if (srcRemaining == 1) Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns>Length of the first bitmask</returns>
    private static int FillMaskArray<T, TVector, TMask>(
        T quote,
        int quoteCount,
        ReadOnlySpan<T> source,
        Span<TMask> destination)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<TMask>
        where TMask : unmanaged, IUnsignedNumber<TMask>, IBinaryInteger<TMask>
    {
        Debug.Assert(source.Length >= TVector.Count); // source must fit at least one vector
        Debug.Assert(TVector.Count * destination.Length >= source.Length); // all bits of the source must fit
        Debug.Assert(BitOperations.IsPow2(TVector.Count)); // vector size must be a power of 2
        Debug.Assert(!destination.ContainsAnyExcept(TMask.Zero)); // destination must be zeroed
        Debug.Assert(quoteCount % 2 == 0); // quotes must be in pairs

        TVector quoteVec = TVector.Create(quote);

        ref T src = ref MemoryMarshal.GetReference(source);
        ref TMask mask = ref MemoryMarshal.GetReference(destination);

        int offsetFromEnd = source.Length & (TVector.Count - 1);
        int firstMaskLength = offsetFromEnd == 0 ? TVector.Count : offsetFromEnd;
        nuint srcPos = 0;
        nuint maskPos = 0;
        nint remaining = source.Length;
        TMask carry = TMask.Zero;

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
            firstMaskLength,
            true);

        // Process remaining vectors. if we run out of quotes before end of data, we can leave the rest at zero
        // if we run out of data before quotes, the field is invalid
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

        if (quoteCount != 0) Invalid(source, Reason.QuoteCount);
        return firstMaskLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessVector<T, TVector, TMask>(
        ref int quoteCount,
        ref T src,
        ref TMask mask,
        ref nuint srcPos,
        ref nuint maskPos,
        ref nint remaining,
        ref TMask carry,
        TVector quoteVec,
        int advanceBy,
        bool isFirstVector)
        where T : unmanaged, IBinaryInteger<T>
        where TVector : struct, ISimdVector<T, TVector>, IMoveMask<TMask>
        where TMask : unmanaged, IUnsignedNumber<TMask>, IBinaryInteger<TMask>
    {
        TVector current = TVector.LoadUnaligned(ref src, srcPos);
        TVector eq = TVector.Equals(current, quoteVec);

        TMask bits = eq.MoveMask();

        if (isFirstVector)
        {
            bits <<= (TVector.Count - advanceBy);
        }

        TMask shifted = bits << 1 | carry;
        Unsafe.Add(ref mask, maskPos) = bits & shifted;

        quoteCount -= int.CreateTruncating(TMask.PopCount(bits));
        carry = bits >> 31;
        srcPos += (nuint)advanceBy;
        remaining -= advanceBy;
        maskPos++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Copy<T>(ref T src, nint srcIndex, ref T dst, nint dstIndex, nint length)
        where T : unmanaged
    {
        Debug.Assert(!Unsafe.IsNullRef(ref src));
        Debug.Assert(srcIndex >= 0);
        Debug.Assert(!Unsafe.IsNullRef(ref dst));
        Debug.Assert(dstIndex >= 0);
        Debug.Assert(length >= 0);

        Unsafe.CopyBlockUnaligned(
            Unsafe.AsPointer(ref Unsafe.Add(ref dst, dstIndex)),
            Unsafe.AsPointer(ref Unsafe.Add(ref src, srcIndex)),
            (uint)length * (uint)Unsafe.SizeOf<T>());
    }

    [DoesNotReturn]
    static void Invalid<T>(
        ReadOnlySpan<T> input,
        Reason reason,
        [CallerMemberName] string memberName = "")
    {
        throw new InvalidOperationException($"Invalid unescape in {memberName}: {reason} {input.ToString()}");
    }

    enum Reason
    {
        QuoteCount,
        NonConsecutive,
    }
}
