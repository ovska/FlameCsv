using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal sealed class SimdTokenizer<T, TNewline, TVector>(CsvOptions<T> options) : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
    where TVector : struct, IAsciiVector<TVector>
{
    // leave space for 2 vectors;
    // vector count to avoid reading past the buffer
    // vector count for prefetching
    // and 1 for reading past the current vector to check two token sequences
    private static int EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (TVector.Count * 2) + (TNewline.IsCRLF ? 1 : 0);
    }

    public override int PreferredLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TVector.Count * 4;
    }

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex)
    {
        if ((data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)EndOffset;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        // search space of Meta is set to vector length from actual so we don't need to do bounds checks in the loops
        // ensure the worst case doesn't read past the end (e.g. data ends in Vector.Count delimiters)
        Debug.Assert(metaBuffer.Length >= TVector.Count);

        scoped ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        scoped ref readonly Meta metaEnd = ref Unsafe.Add(
            ref MemoryMarshal.GetReference(metaBuffer),
            metaBuffer.Length - TVector.Count
        );

        // load the constants into registers
        T quote = _quote;
        TVector delimiterVec = TVector.Create(_delimiter);
        TVector quoteVec = TVector.Create(_quote);
        uint quotesConsumed = 0;

        TVector nextVector = TVector.LoadUnaligned(ref first, runningIndex);

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd) && runningIndex <= searchSpaceEnd)
        {
            // prefetch the next vector so we can process the current without waiting for it to load
            TVector vector = nextVector;
            nextVector = TVector.LoadUnaligned(ref first, runningIndex + (nuint)TVector.Count);

            TVector hasDelimiter = TVector.Equals(vector, delimiterVec);
            TVector hasQuote = TVector.Equals(vector, quoteVec);
            TVector hasNewline = TNewline.HasNewline(vector);
            TVector hasAny = hasNewline | hasDelimiter | hasQuote;

            nuint maskAny = hasAny.ExtractMostSignificantBits();
            nuint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();

            // nothing of note in this slice
            if (maskAny == 0)
            {
                goto ContinueRead;
            }

            // we have read quotes, must use ParseAny to handle them (or possibly we can skip the whole vector)
            if (quotesConsumed != 0)
            {
                goto TrySkipQuoted;
            }

            // only delimiters
            if (maskDelimiter != maskAny)
            {
                goto HandleNewlines;
            }

            HandleDelimiters:
            currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);
            runningIndex += (nuint)TVector.Count;
            continue;

            HandleNewlines:
            nuint maskNewlineOrDelimiter = (hasNewline | hasDelimiter).ExtractMostSignificantBits();

            // if vector is not only newlines or delimiters, go to quote handling
            if (maskNewlineOrDelimiter != maskAny)
            {
                goto HandleAny;
            }

            if (maskDelimiter != 0)
            {
                // Check if the sets are fully separated
                nuint maskNewline = maskNewlineOrDelimiter & ~maskDelimiter;

                if (Bithacks.AllBitsBefore(maskDelimiter, maskNewline))
                {
                    currentMeta = ref ParseDelimiters(maskDelimiter, runningIndex, ref currentMeta);
                    maskNewlineOrDelimiter = maskNewline;
                    // Fall through to parse line ends
                }
                else if (Bithacks.AllBitsBefore(maskNewline, maskDelimiter))
                {
                    currentMeta = ref ParseLineEnds(
                        maskNewline,
                        ref first,
                        ref runningIndex,
                        ref currentMeta,
                        ref nextVector
                    );

                    goto HandleDelimiters;
                }
                else
                {
                    // bits are interleaved, handle the mixed case
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

            TrySkipQuoted:
            Debug.Assert(quotesConsumed > 0, "quotesConsumed should be greater than 0 here");

            // if there are dangling quotes but the current vector has none, we must be in a string.
            // check if we can skip it
            if (hasQuote == TVector.Zero && quotesConsumed % 2 == 1)
            {
                //              -- current --
                // [1, "John ""][The Amazing]["" Doe", 00]
                goto ContinueRead;
            }

            // the current vector has no quotes, but a string might have just ended
            // as the other paths don't use quotesConsumed and this case is exceedlingly rare (0,028%)
            // just use the ParseAny path
            //              -- current --
            // [John, "Doe"][, 123, 4567]
            // any combination of delimiters, quotes, and newlines
            HandleAny:
            currentMeta = ref ParseAny(
                maskAny,
                ref first,
                ref runningIndex,
                ref currentMeta,
                quote,
                ref quotesConsumed,
                ref nextVector
            );

            ContinueRead:
            runningIndex += (nuint)TVector.Count;
        }

        nint byteOffset = Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta);
        return (int)byteOffset / Unsafe.SizeOf<Meta>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta ParseDelimiters(nuint mask, nuint runningIndex, ref Meta currentMeta)
    {
        uint count = (uint)BitOperations.PopCount(mask);

        // we might write more values than we have bits in the mask, but the meta buffer always has space,
        // and we return the correct reference at the end. this way we can avoid branching 99%+ of the time here
        Unsafe.Add(ref currentMeta, 0u) = Meta.Plain((int)runningIndex + BitOperations.TrailingZeroCount(mask));
        mask &= (mask - 1);
        Unsafe.Add(ref currentMeta, 1u) = Meta.Plain((int)runningIndex + BitOperations.TrailingZeroCount(mask));
        mask &= (mask - 1);
        Unsafe.Add(ref currentMeta, 2u) = Meta.Plain((int)runningIndex + BitOperations.TrailingZeroCount(mask));
        mask &= (mask - 1);

        // unrolling to 5 is faster for 256 bit vectors, while 3 is optimal for 128 bit vectors
        // 512 bit vectors aren't used currently as 256 bit vectors are faster even on Avx512BW
        if (TVector.Count > 16)
        {
            Unsafe.Add(ref currentMeta, 3u) = Meta.Plain((int)runningIndex + BitOperations.TrailingZeroCount(mask));
            mask &= (mask - 1);
            Unsafe.Add(ref currentMeta, 4u) = Meta.Plain((int)runningIndex + BitOperations.TrailingZeroCount(mask));
            mask &= (mask - 1);
        }

        // don't store this into a local, it doesn't need to live in a register
        if (count > (TVector.Count > 16 ? 5u : 3u))
        {
            // for some reason this is faster than incrementing a pointer
            uint index = TVector.Count > 16 ? 5u : 3u;

            do
            {
                int offset = BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                Unsafe.Add(ref currentMeta, index++) = Meta.Plain((int)runningIndex + offset);
            } while (mask != 0);
        }

        return ref Unsafe.Add(ref currentMeta, count);
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
        int offset;
        bool isMultitoken;

        do
        {
            offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            // no need to call IsNewline(), the mask contains only newlines
            // this whole method should be exceedingly rare anyways
            isMultitoken = TNewline.IsMultitoken(ref Unsafe.Add(ref first, runningIndex + (nuint)offset));

            currentMeta = Meta.Plain((int)runningIndex + offset, isEOL: true, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);

            if (TNewline.IsCRLF && isMultitoken) // runtime constant
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // Vector boundary crossing is very rare (e.g. 1/16 or 1/32 or 1/64)
        // offset will be 31 only if the final bit was a CR; a block ending in CRLF will have offset of 30
        if (TNewline.IsCRLF && isMultitoken && offset == TVector.Count - 1)
        {
            nextVector = TVector.LoadUnaligned(ref first, runningIndex + (nuint)TVector.Count + 1);
            runningIndex += 1;
        }

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
        bool isMultitoken;

        do
        {
            offset = BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit

            isEOL = TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + (nuint)offset), out isMultitoken);

            currentMeta = Meta.Plain((int)runningIndex + offset, isEOL, TNewline.GetLength(isMultitoken));
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);

            // assume EOL is rarer than delimiters. OffsetFromEnd is a runtime constant
            if (TNewline.IsCRLF && isEOL && isMultitoken)
            {
                mask &= (mask - 1);
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // Vector boundary crossing is very rare (e.g. 1/16 or 1/32 or 1/64)
        // offset will be e.g. 31 only if the final bit was a delimiter; a block ending in CRLF
        // will have offset of 30 instead
        if (TNewline.IsCRLF && isEOL && isMultitoken && offset == TVector.Count - 1)
        {
            nextVector = TVector.LoadUnaligned(ref first, runningIndex + (nuint)TVector.Count + 1);
            runningIndex += 1;
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
            if (TNewline.IsCRLF && isEOL && isMultitoken)
            {
                // clear the next bit, or adjust the index if we crossed a vector boundary
                mask &= (mask - 1);

                if (offset == TVector.Count - 1)
                {
                    nextVector = TVector.LoadUnaligned(ref first, runningIndex + (nuint)TVector.Count + 1);
                    runningIndex += 1;
                }
            }
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // can't lift out the EOL check to here, as we might have a quote after the last EOL leaving it in the wrong state

        return ref currentMeta;
    }
}
