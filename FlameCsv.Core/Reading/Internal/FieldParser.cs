using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

/*
 * For general purpose data with occasional quotes:
 * 50% of vectors had only delimiters.
 * 30% of vectors had quotes or are continuations from previous string.
 * 7% of vectors had delimiters followed by newline(s) (worthwhile optimization, thanks Sep).
 * 5% of vectors were in the middle of a string and had no quotes (can be skipped).
 * 4% of vectors had nothing in them.
 * 2% of vectors had delimiters and newlines mixed in order (surprising).
 * 1,5% of vectors had newline(s) before delimiter(s) (not worth to pursue further).
 * 0,6% of vectors had only newlines (very small %).
 *
 * For very short fields without quotes, only 1,3% of vectors have only delimiters.
 * The Rest is mixed delimiters and newlines.
 *
 * The sequential parser is always slower, no matter how small the input.
 */

[SkipLocalsInit]
[SuppressMessage("ReSharper", "InlineTemporaryVariable")]
internal static class FieldParser<T, TNewline, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline<T, TVector>, allows ref struct
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Core(
        T delimiterArg,
        T quoteArg,
        scoped TNewline newlineArg,
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBuffer)
    {
        // search space of T is set to 1 vector less, possibly leaving space for a newline token so we don't need
        // to do bounds checks in the loops
        ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = 0;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)(TVector.Count * 2) - TNewline.OffsetFromEnd;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        // search space of Meta is set to vector length from actual so we don't need to do bounds checks in the loops
        scoped ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        scoped ref readonly Meta metaEnd = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(metaBuffer),
            metaBuffer.Length - (TVector.Count * 2)); // the worst case: data ends in Vector.Count delimiters

        // load the constants into registers
        T delimiter = delimiterArg;
        T quote = quoteArg;
        TNewline newline = newlineArg;
        TVector delimiterVec = TVector.Create(delimiterArg);
        TVector quoteVec = TVector.Create(quoteArg);
        uint quotesConsumed = 0;

        TVector nextVector = TVector.LoadUnaligned(in first, runningIndex);

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd) && runningIndex <= searchSpaceEnd)
        {
            // prefetch the next vector so we can process the current without waiting for it to load
            TVector vector = nextVector;
            nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);

            TVector hasDelimiter = TVector.Equals(vector, delimiterVec);
            TVector hasQuote = TVector.Equals(vector, quoteVec);
            TVector hasNewline = newline.HasNewline(vector);
            TVector hasNewlineOrDelimiter = hasNewline | hasDelimiter;
            TVector hasAny = hasQuote | hasNewlineOrDelimiter;

            nuint maskAny = hasAny.ExtractMostSignificantBits();

            // nothing of note in this slice
            if (maskAny == 0)
            {
                runningIndex += (nuint)TVector.Count;
                continue;
            }

            // TODO: profile with different data; if quote count is not 0, it might be faster to just go to ParseAny

            nuint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();

            // only delimiters? skip this if there are any quotes in the current field
            if (maskDelimiter == maskAny)
            {
                if (quotesConsumed != 0) goto TrySkipQuoted;
                currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);
                runningIndex += (nuint)TVector.Count;
                continue;
            }

            nuint maskNewlineOrDelimiter = hasNewlineOrDelimiter.ExtractMostSignificantBits();

            if (maskNewlineOrDelimiter == maskAny)
            {
                if (quotesConsumed != 0) goto TrySkipQuoted;

                if (maskDelimiter != 0)
                {
                    nuint maskNewline = maskNewlineOrDelimiter & ~maskDelimiter;
                    int indexNewline = BitOperations.TrailingZeroCount(maskNewline);

                    // check if the delimiters and newlines are interleaved
                    if ((Unsafe.SizeOf<nuint>() * 8 - 1) - BitOperations.LeadingZeroCount(maskDelimiter) <
                        indexNewline)
                    {
                        // all delimiters are before any of the newlines
                        Debug.Assert(quotesConsumed == 0);
                        currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);

                        // fall through to parse line ends
                        maskNewlineOrDelimiter = maskNewline;
                    }
                    else
                    {
                        Debug.Assert(quotesConsumed == 0);
                        currentMeta = ref ParseDelimitersAndLineEnds(
                            maskNewlineOrDelimiter,
                            ref first,
                            ref runningIndex,
                            ref currentMeta,
                            delimiter,
                            in newline,
                            ref nextVector);
                        runningIndex += (nuint)TVector.Count;
                        continue;
                    }
                }

                currentMeta = ref ParseLineEnds(
                    maskNewlineOrDelimiter,
                    ref first,
                    ref runningIndex,
                    ref currentMeta,
                    in newline,
                    ref nextVector);
                runningIndex += (nuint)TVector.Count;
                continue;
            }

        ParseAny:
            // any combination of delimiters, quotes, and newlines
            currentMeta = ref ParseAny(
                maskAny,
                ref first,
                ref runningIndex,
                ref currentMeta,
                delimiter,
                quote,
                in newline,
                ref quotesConsumed,
                ref nextVector);

            runningIndex += (nuint)TVector.Count;
            continue;

        TrySkipQuoted:
            // there are unresolved quotes but the current vector had none
            Debug.Assert(hasQuote == TVector.Zero);

            // verifiably in a string?
            if (quotesConsumed == 1)
            {
                runningIndex += (nuint)TVector.Count;
                continue;
            }

            // we are in a string, but we can't know if we are in a string, or one just ended in the last vector
            Debug.Assert(quotesConsumed > 1);
            goto ParseAny;
        }

        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta) /
            Unsafe.SizeOf<Meta>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimiters(
        nuint mask,
        nuint runningIndex,
        ref Meta currentMeta)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            currentMeta = Meta.Plain((int)runningIndex + offset);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseLineEnds(
        nuint mask,
        scoped ref T first,
        scoped ref nuint runningIndex,
        ref Meta currentMeta,
        scoped ref readonly TNewline newline,
        ref TVector nextVector)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (newline.IsNewline(ref Unsafe.Add(ref first, runningIndex + (nuint)offset)))
            {
                currentMeta = Meta.Plain((int)runningIndex + offset, isEOL: true, TNewline.Length);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);

                if (TNewline.OffsetFromEnd != 0)
                {
                    // clear the next bit, or adjust the index if we crossed a vector boundary
                    mask &= (mask - 1);

                    if (offset == TVector.Count - 1)
                    {
                        runningIndex += TNewline.OffsetFromEnd;
                        nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
                    }
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimitersAndLineEnds(
        nuint mask,
        scoped ref T first,
        scoped ref nuint runningIndex,
        ref Meta currentMeta,
        T delimiter,
        scoped ref readonly TNewline newline,
        ref TVector nextVector)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            // this can only return false for uncommon two-token newline setups, like a CR followed by a delimiter
            if (newline.IsDelimiterOrNewline(
                    delimiter,
                    ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                    out bool isEOL))
            {
                currentMeta = Meta.Plain((int)runningIndex + offset, isEOL, TNewline.Length);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);

                if (TNewline.OffsetFromEnd != 0 && isEOL)
                {
                    // clear the next bit, or adjust the index if we crossed a vector boundary
                    mask &= (mask - 1);

                    if (offset == TVector.Count - 1)
                    {
                        runningIndex += TNewline.OffsetFromEnd;
                        nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
                    }
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseAny(
        nuint mask,
        ref T first,
        scoped ref nuint runningIndex,
        ref Meta currentMeta,
        T delimiter,
        T quote,
        scoped ref readonly TNewline newline,
        scoped ref uint quotesConsumed,
        ref TVector nextVector)
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (Unsafe.Add(ref first, runningIndex + (nuint)offset) == quote)
            {
                ++quotesConsumed;
            }
            else if ((quotesConsumed & 1) == 0 &&
                     newline.IsDelimiterOrNewline(
                         delimiter,
                         ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                         out bool isEOL))
            {
                currentMeta = Meta.RFC((int)runningIndex + offset, quotesConsumed, isEOL, TNewline.Length);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                quotesConsumed = 0;

                if (TNewline.OffsetFromEnd != 0 && isEOL)
                {
                    // clear the next bit, or adjust the index if we crossed a vector boundary
                    mask &= (mask - 1);

                    if (offset == TVector.Count - 1)
                    {
                        runningIndex += TNewline.OffsetFromEnd;
                        nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
                    }
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }
}

/*
    private static unsafe ref Meta EnsureAlignment(
        nuint searchSpaceEnd,
        T quote,
        T delimiter,
        TVector quoteVec,
        TVector delimiterVec,
        scoped ref readonly TNewline newline,
        scoped ref T first,
        ref Meta currentMeta,
        scoped ref nuint runningIndex,
        scoped ref uint quotesConsumed,
        ref TVector nextVector)
    {
        byte* currentAddress = (byte*)Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex));

        // if the address is not aligned, we need to read a single vector's worth of data
        if (((nuint)currentAddress & (nuint)(TVector.Count - 1)) != 0)
        {
            bool crossedVectorBoundary = false;

            byte* aligned = (byte*)(((nuint)currentAddress + (nuint)TVector.Count) & ~(nuint)(TVector.Count - 1));

            // early exit, should never happen?
            if (!Unsafe.IsAddressLessThan(ref Unsafe.AsRef<T>(aligned), ref Unsafe.Add(ref first, searchSpaceEnd)))
            {
                runningIndex = searchSpaceEnd + 1;
                return ref currentMeta;
            }

            // read first vector's worth of data
            TVector vector = TVector.LoadUnaligned(in first, runningIndex);
            TVector hasQuote = TVector.Equals(vector, quoteVec);
            TVector hasNewlineOrDelimiter = TVector.Equals(vector, delimiterVec) | newline.HasNewline(vector);
            TVector hasAny = hasQuote | hasNewlineOrDelimiter;

            // mask out the bits that spill over to the aligned address, so we only process them once
            nuint offsetElements = (nuint)(aligned - currentAddress) / (nuint)Unsafe.SizeOf<T>();
            nuint mask = hasAny.ExtractMostSignificantBits() & (((nuint)1UL << (int)offsetElements) - 1);

            while (mask != 0) // no bounds-check, meta-buffer always has space for a full vector
            {
                int offset = BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1); // clear lowest bit

                if (Unsafe.Add(ref first, runningIndex + (nuint)offset) == quote)
                {
                    ++quotesConsumed;
                }
                else if ((quotesConsumed & 1) == 0 &&
                         newline.IsDelimiterOrNewline(
                             delimiter,
                             ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                             out bool isEOL))
                {
                    currentMeta = Meta.RFC((int)runningIndex + offset, quotesConsumed, isEOL, TNewline.Length);
                    currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                    quotesConsumed = 0;

                    if (TNewline.OffsetFromEnd != 0 && isEOL)
                    {
                        // clear the next bit, or adjust the index if we crossed a vector boundary
                        mask &= (mask - 1);

                        if (offset == TVector.Count - 1)
                        {
                            crossedVectorBoundary = true;
                        }
                    }
                }
            }

            Debug.Assert((nuint)aligned % (ulong)TVector.Count == 0);

            runningIndex += offsetElements;
            nextVector = TVector.LoadAligned(ref first, runningIndex);

            if (crossedVectorBoundary)
            {
                nextVector = nextVector.WithZeroFirstElement();
            }
        }

        return ref currentMeta;
    }
*/
