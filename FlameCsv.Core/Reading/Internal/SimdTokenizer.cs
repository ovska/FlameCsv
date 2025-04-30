using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

/*
 * For general purpose data with occasional quotes (256-bit vectors):
 * 50% of vectors had only delimiters.
 * 30% of vectors had quotes (forced ParseAny)
 * 7% of vectors had delimiters followed by newline(s)
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
 *
 * Failed attempts at optimization:
 * - double-laning 256-bit vectors
 * - using a generic mask size instead of nuint
 * - using popcount and unrolling ParseDelimiters when possible
 * - creating a generic "bool" for whether the data has quotes -> slower due to having to scan the whole data first
 * - BitHacks.FindQuoteMask and zero out bits between quotes -> parsing is too fast, the extra instructions are not worth it
 *
 * Still to do:
 * - Loading comparisons from the vector instead of original data (maybe not possible due to newline boundaries?)
 */

[SkipLocalsInit]
[SuppressMessage("ReSharper", "InlineTemporaryVariable")]
internal sealed class SimdTokenizer<T, TNewline, TVector>(CsvOptions<T> options) : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline<T, TVector>
    where TVector : struct, ISimdVector<T, TVector>
{
    private static int EndOffset => (TVector.Count * 2) + (int)TNewline.OffsetFromEnd;

    public override int PreferredLength => TVector.Count * 4;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex)
    {
        if ((data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        // search space of T is set to 1 vector less, possibly leaving space for a newline token so we don't need
        // to do bounds checks in the loops
        scoped ref T first =
            ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)EndOffset;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        // search space of Meta is set to vector length from actual so we don't need to do bounds checks in the loops
        // ensure the worst case doesn't read past the end (data ends in Vector.Count delimiters)
        scoped ref Meta currentMeta =
            ref MemoryMarshal.GetReference(metaBuffer);
        scoped ref readonly Meta metaEnd = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(metaBuffer),
            metaBuffer.Length - (TVector.Count * 2)
        );

        // load the constants into registers
        T quote = _quote;
        TVector delimiterVec = TVector.Create(_delimiter);
        TVector quoteVec = TVector.Create(_quote);
        TNewline.Load(out TVector newline1, out TVector newline2);
        uint quotesConsumed = 0;

        TVector nextVector = TVector.LoadUnaligned(in first, runningIndex);

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd) && runningIndex <= searchSpaceEnd)
        {
            // prefetch the next vector so we can process the current without waiting for it to load
            TVector vector = nextVector;
            nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);

            TVector hasDelimiter = TVector.Equals(vector, delimiterVec);
            TVector hasQuote = TVector.Equals(vector, quoteVec);
            TVector hasNewline = TNewline.HasNewline(vector, newline1, newline2);
            TVector hasAny = hasNewline | hasDelimiter | hasQuote;

            nuint maskAny = hasAny.ExtractMostSignificantBits();

            // nothing of note in this slice
            if (maskAny == 0)
            {
                goto ContinueRead;
            }

            nuint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();

            // only delimiters
            if (maskDelimiter == maskAny)
            {
                if (quotesConsumed != 0)
                    goto TrySkipQuoted;
                currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);
                goto ContinueRead;
            }

            nuint maskNewlineOrDelimiter = (hasNewline | hasDelimiter).ExtractMostSignificantBits();

            // only newlines or delimiters
            if (maskNewlineOrDelimiter == maskAny)
            {
                if (quotesConsumed != 0)
                    goto TrySkipQuoted;

                if (maskDelimiter != 0)
                {
#if true
                    nuint maskNewline = maskNewlineOrDelimiter & ~maskDelimiter;
                    int indexNewline = BitOperations.TrailingZeroCount(maskNewline);

                    // check if the delimiters and newlines are interleaved
                    if ((nuint.Size * 8 - 1) - BitOperations.LeadingZeroCount(maskDelimiter) < indexNewline)
                    {
                        // all delimiters are before any of the newlines
                        currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);

                        // fall through to parse line ends
                        maskNewlineOrDelimiter = maskNewline;
                    }
                    else
#endif
                    {
                        Debug.Assert(quotesConsumed == 0);
                        currentMeta = ref ParseDelimitersAndLineEnds(
                            maskNewlineOrDelimiter,
                            ref first,
                            ref runningIndex,
                            ref currentMeta,
                            ref nextVector
                        );
                        goto ContinueRead;
                    }
                }

                currentMeta = ref ParseLineEnds(
                    maskNewlineOrDelimiter,
                    ref first,
                    ref runningIndex,
                    ref currentMeta,
                    ref nextVector
                );
                goto ContinueRead;
            }

            // any combination of delimiters, quotes, and newlines
            currentMeta = ref ParseAny(
                maskAny,
                ref first,
                ref runningIndex,
                ref currentMeta,
                quote,
                ref quotesConsumed,
                ref nextVector
            );
            goto ContinueRead;

            TrySkipQuoted:
            // there are unresolved quotes but the current vector had none
            Debug.Assert(hasQuote == TVector.Zero);

            // verifiably in a string?
            if (quotesConsumed % 2 == 1)
            {
                goto ContinueRead;
            }

            // the current vector has no quotes, but a string might have just ended, e.g.:
            // [John, "Doe"]
            // [, 123, 4567]
            // use a separate loop so the branch predictor can create a separate table for ParseAny without quotes
            currentMeta = ref ParseAnyNoQuotes(
                maskAny,
                ref first,
                ref runningIndex,
                ref currentMeta,
                ref quotesConsumed,
                ref nextVector
            );

            ContinueRead:
            runningIndex += (nuint)TVector.Count;
        }

        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta)
            / Unsafe.SizeOf<Meta>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimiters(nuint mask, nuint runningIndex, ref Meta currentMeta)
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
        ref TVector nextVector
    )
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            // no need to call IsNewline(), the mask contains only newlines
            // this whole method should be exceedingly rare anyways
            bool isMultitoken = TNewline.IsMultitoken(ref Unsafe.Add(ref first, runningIndex + (nuint)offset));

            currentMeta = Meta.Plain((int)runningIndex + offset, isEOL: true, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);

            if (TNewline.OffsetFromEnd != 0 && isMultitoken) // runtime constant
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);

                // Branch predictor expects forward branches to be NOT taken (common case)
                // Vector boundary crossing is very rare (e.g. 1/16 or 1/32 or 1/64)
                if (offset == TVector.Count - 1)
                {
                    // do not reorder
                    runningIndex += TNewline.OffsetFromEnd;
                    nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
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
        ref TVector nextVector
    )
    {
        int offset;
        bool isEOL;

        do
        {
            offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            // this can only return false for pathological data, e.g. \r followed by \r or comma
            isEOL = TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + (nuint)offset), out bool isMultitoken);

            currentMeta = Meta.Plain((int)runningIndex + offset, isEOL, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);

            // assume EOL is rarer than delimiters. OffsetFromEnd is a runtime constant
            if (TNewline.OffsetFromEnd != 0 && isEOL && isMultitoken)
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // Branch predictor expects forward branches to be NOT taken (common case)
        // Vector boundary crossing is very rare (e.g. 1/16 or 1/32 or 1/64)
        if (TNewline.OffsetFromEnd != 0 && isEOL && offset == TVector.Count - 1)
        {
            // do not reorder
            runningIndex += TNewline.OffsetFromEnd;
            nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
        }

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseAny(
        nuint mask,
        ref T first,
        scoped ref nuint runningIndex,
        ref Meta currentMeta,
        T quote,
        scoped ref uint quotesConsumed,
        ref TVector nextVector
    )
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            if (Unsafe.Add(ref first, runningIndex + (nuint)offset) == quote)
            {
                ++quotesConsumed;
                continue;
            }

            if (quotesConsumed % 2 == 1)
            {
                continue;
            }

            bool isEOL = TNewline.IsNewline(
                ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                out bool isMultitoken
            );

            currentMeta = Meta.RFC((int)runningIndex + offset, quotesConsumed, isEOL, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
            quotesConsumed = 0;

            // assume EOL is rarer than delimiters. OffsetFromEnd is a runtime constant
            if (TNewline.OffsetFromEnd != 0 && isEOL && isMultitoken)
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);

                if (offset == TVector.Count - 1)
                {
                    // do not reorder
                    runningIndex += TNewline.OffsetFromEnd;
                    nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseAnyNoQuotes(
        nuint mask,
        ref T first,
        scoped ref nuint runningIndex,
        ref Meta currentMeta,
        scoped ref uint quotesConsumed,
        ref TVector nextVector
    )
    {
        do
        {
            int offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            bool isEOL = TNewline.IsNewline(
                ref Unsafe.Add(ref first, runningIndex + (nuint)offset),
                out bool isMultitoken
            );

            currentMeta = Meta.RFC((int)runningIndex + offset, quotesConsumed, isEOL, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
            quotesConsumed = 0;

            // assume EOL is rarer than delimiters. OffsetFromEnd is a runtime constant
            if (TNewline.OffsetFromEnd != 0 && isEOL && isMultitoken)
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);

                if (offset == TVector.Count - 1)
                {
                    // do not reorder
                    runningIndex += TNewline.OffsetFromEnd;
                    nextVector = TVector.LoadUnaligned(in first, runningIndex + (nuint)TVector.Count);
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        return ref currentMeta;
    }
}
