using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal sealed class SimdTokenizer<T, TNewline>(CsvOptions<T> options) : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    private static int EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count * 2;
    }

    private static int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count;
    }

    public override int PreferredLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count * 4;
    }

    public override int MinimumFieldBufferSize => MaxFieldsPerIteration;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    private static Vector256<byte> ZeroFirst
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256.Create(-256L, ~0L, ~0L, ~0L).AsByte();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data)
    {
        if ((data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)EndOffset;

        scoped ref uint dstField = ref MemoryMarshal.GetReference(buffer.Fields);
        scoped ref byte dstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
        nuint fieldIndex = 0;
        nuint fieldEnd = Math.Max(0, (nuint)buffer.Fields.Length - (nuint)MaxFieldsPerIteration);

        // ensure the worst case doesn't read past the end (e.g. data ends in Vector.Count delimiters)
        // we do this so there are no bounds checks in the loops
        Debug.Assert(searchSpaceEnd < (nuint)data.Length);
        Debug.Assert(buffer.Fields.Length >= Vector256<byte>.Count);
        Debug.Assert(buffer.Quotes.Length >= Vector256<byte>.Count);

        // load the constants into registers
        T delimiter = _delimiter;
        T quote = _quote;
        uint quotesConsumed = 0;
        uint quoteCarry = 0;

        Vector256<byte> delimiterVec = AsciiVector.Create(delimiter);
        Vector256<byte> quoteVec = AsciiVector.Create(quote);
        Vector256<byte> lfVec = AsciiVector.Create((byte)'\n');
        Vector256<byte> crVec = TNewline.IsCRLF ? AsciiVector.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load(ref first, runningIndex);
        Vector256<byte> hasNewline = TNewline.IsCRLF
            ? Vector256.Equals(vector, lfVec) | Vector256.Equals(vector, crVec)
            : Vector256.Equals(vector, lfVec);
        Vector256<byte> hasDelimiter = Vector256.Equals(vector, delimiterVec);
        Vector256<byte> hasQuote = Vector256.Equals(vector, quoteVec);
        Vector256<byte> hasControl = hasNewline | hasDelimiter;

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
        {
            if (typeof(T) == typeof(char) && Sse.IsSupported)
            {
                uint prefetch = Avx512BW.IsSupported ? 192u : 384u;

                unsafe
                {
                    Sse.Prefetch0(Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex + prefetch)));
                }
            }

            uint maskControl = hasControl.ExtractMostSignificantBits();
            uint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            // prefetch the next vector so we can process the current without waiting for it to load
            vector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

            // nothing of note in this slice
            if (maskControl == 0)
            {
                goto ContinueRead;
            }

            // we have read quotes, must use ParseAny to handle them (or possibly we can skip the whole vector)
            if ((maskQuote | quoteCarry | quotesConsumed) != 0)
            {
                goto TrySkipQuoted;
            }

            // only delimiters
            if (maskDelimiter != maskControl)
            {
                goto HandleNewlines;
            }

            HandleDelimiters:
            ParseDelimiters(maskDelimiter, runningIndex, ref fieldIndex, ref dstField);
            goto ContinueRead;

            HandleNewlines:
            uint maskNewlineOrDelimiter = maskControl & ~maskQuote;

            // if vector is not only newlines or delimiters, go to quote handling
            if (maskNewlineOrDelimiter != maskControl)
            {
                goto HandleAny;
            }

            uint maskNewline;

            if (maskDelimiter != 0)
            {
                // Check if the sets are fully separated
                maskNewline = maskNewlineOrDelimiter & ~maskDelimiter;

                if (Bithacks.AllBitsBefore(maskDelimiter, maskNewline))
                {
                    // don't try to goto and branch in HandleDelimiters here, slows the loop by a few %
                    ParseDelimiters(maskDelimiter, runningIndex, ref fieldIndex, ref dstField);
                    maskNewlineOrDelimiter = maskNewline;
                    // Fall through to parse line ends
                }
                else if (Bithacks.AllBitsBefore(maskNewline, maskDelimiter))
                {
                    ParseLineEnds(maskNewline, ref first, runningIndex, ref fieldIndex, ref dstField, ref vector);

                    goto HandleDelimiters;
                }
                else
                {
                    // bits are interleaved, handle the mixed case
                    Debug.Assert(quotesConsumed == 0);
                    ParseDelimitersAndLineEnds(
                        maskNewlineOrDelimiter,
                        ref first,
                        delimiter,
                        runningIndex,
                        ref fieldIndex,
                        ref dstField,
                        ref vector
                    );
                    goto ContinueRead;
                }
            }

            ParseLineEnds(maskNewlineOrDelimiter, ref first, runningIndex, ref fieldIndex, ref dstField, ref vector);
            goto ContinueRead;

            TrySkipQuoted:
            // if there are dangling quotes but the current vector has none, we must be in a string.
            // check if we can skip it
            if (quotesConsumed % 2 == 1 && hasQuote == Vector256<byte>.Zero)
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
            uint quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            ParseAny(
                maskAny: maskControl,
                maskQuote: maskQuote,
                first: ref first,
                runningIndex: runningIndex,
                fieldIndex: ref fieldIndex,
                fieldRef: ref dstField,
                quoteRef: ref dstQuote,
                delimiter: delimiter,
                quotesConsumed: ref quotesConsumed,
                nextVector: ref vector
            );

            ContinueRead:
            runningIndex += (nuint)Vector256<byte>.Count;
            hasNewline = TNewline.IsCRLF
                ? Vector256.Equals(vector, lfVec) | Vector256.Equals(vector, crVec)
                : Vector256.Equals(vector, lfVec);
            hasDelimiter = Vector256.Equals(vector, delimiterVec);
            hasQuote = Vector256.Equals(vector, quoteVec);
            hasControl = hasNewline | hasDelimiter;
        }

        return (int)fieldIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimiters(uint mask, nuint runningIndex, ref nuint fieldIndex, ref uint fieldRef)
    {
        uint count = (uint)BitOperations.PopCount(mask);

        // on 128bit vectors 3 is optimal; revisit if we change width
        const uint unrollCount = 5;

        uint increment = (uint)runningIndex;
        ref uint dst = ref Unsafe.Add(ref fieldRef, fieldIndex);

        // we might write more values than we have bits in the mask, but the meta buffer always has space,
        // and we return the correct reference at the end. this way we can avoid branching 99%+ of the time here
        Unsafe.Add(ref dst, 0u) = increment + (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref dst, 1u) = increment + (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref dst, 2u) = increment + (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref dst, 3u) = increment + (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        Unsafe.Add(ref dst, 4u) = increment + (uint)BitOperations.TrailingZeroCount(mask);

        if (count > unrollCount)
        {
            mask &= (mask - 1);

            // for some reason this is faster than incrementing a pointer
            dst = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                dst = increment + offset;
                dst = ref Unsafe.Add(ref dst, 1u);
            } while (mask != 0);
        }

        fieldIndex += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseLineEnds(
        uint mask,
        scoped ref T first,
        nuint runningIndex,
        ref nuint fieldIndex,
        ref uint fieldRef,
        ref Vector256<byte> nextVector
    )
    {
        uint offset;
        FieldFlag flag;

        do
        {
            offset = (uint)BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1); // clear lowest bit
            flag = TNewline.GetKnownNewlineFlag(ref Unsafe.Add(ref first, runningIndex + offset), ref mask);
            Unsafe.Add(ref fieldRef, fieldIndex++) = ((uint)runningIndex + offset) | (uint)flag;
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // put the offset check first as it should be more predictable; kind may be CRLF 5-10% of the time
        if (TNewline.IsCRLF && offset == Vector256<byte>.Count - 1 && flag == FieldFlag.CRLF)
        {
            nextVector &= ZeroFirst;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndLineEnds(
        uint mask,
        scoped ref T first,
        T delimiter,
        nuint runningIndex,
        ref nuint fieldIndex,
        ref uint fieldRef,
        ref Vector256<byte> nextVector
    )
    {
        uint offset;
        FieldFlag isEOL;

        do
        {
            offset = (uint)BitOperations.TrailingZeroCount(mask);
            mask &= (mask - 1);
            uint value = (uint)runningIndex + offset;
            isEOL = TNewline.IsNewline(delimiter, ref Unsafe.Add(ref first, value), ref mask);
            Unsafe.Add(ref fieldRef, fieldIndex++) = value | (uint)isEOL;
        } while (mask != 0); // no bounds-check, meta-buffer always has space for a full vector

        // put the offset check first as it should be more predictable; kind may be CRLF 5-10% of the time
        if (TNewline.IsCRLF && offset == Vector256<byte>.Count - 1 && isEOL == FieldFlag.CRLF)
        {
            nextVector &= ZeroFirst;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseAny(
        uint maskAny,
        uint maskQuote,
        scoped ref T first,
        nuint runningIndex,
        ref nuint fieldIndex,
        ref uint fieldRef,
        ref byte quoteRef,
        T delimiter,
        scoped ref uint quotesConsumed,
        ref Vector256<byte> nextVector
    )
    {
        uint offset = 0;
        FieldFlag flag = default;

        while (maskAny != 0) // do..while won't work as the quote XOR might have cleared the mask
        {
            uint maskUpToPos = Bmi1.GetMaskUpToLowestSetBit(maskAny);
            offset = (uint)BitOperations.TrailingZeroCount(maskAny);

            quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);

            uint value = (uint)runningIndex + offset;
            flag = TNewline.IsNewline(delimiter, ref Unsafe.Add(ref first, value), ref maskAny);

            // TODO: optimize this into the IsNewline method
            if (flag == FieldFlag.CRLF)
            {
                maskUpToPos = (maskUpToPos << 1) | 1;
            }

            // consume masks
            maskAny &= ~maskUpToPos;
            maskQuote &= ~maskUpToPos;

            Field.SaturateTo7Bits(ref quotesConsumed);

            Unsafe.Add(ref fieldRef, fieldIndex) = value | (uint)flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)quotesConsumed;
            quotesConsumed = 0;
            fieldIndex++;
        }

        quotesConsumed += (uint)BitOperations.PopCount(maskQuote);

        if (TNewline.IsCRLF && offset == Vector256<byte>.Count - 1 && flag == FieldFlag.CRLF)
        {
            nextVector &= ZeroFirst;
        }
    }
}
