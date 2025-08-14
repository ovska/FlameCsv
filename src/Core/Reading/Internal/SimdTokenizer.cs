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
        Vector256<byte> hasControl = hasNewline | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals(vector, quoteVec);

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
        {
            // TODO: profile Sse.Prefetch0 on multiple machines

            uint maskControl = hasControl.ExtractMostSignificantBits();
            uint maskDelimiter = hasDelimiter.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            // prefetch the next vector so we can process the current without waiting for it to load
            vector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

            if (maskControl == 0)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                goto ContinueRead;
            }

            if ((quoteCarry | quotesConsumed | maskQuote) != 0)
            {
                goto HandleAny;
            }

            uint controlCount = TNewline.IsCRLF ? 0 : (uint)BitOperations.PopCount(maskControl);

            // check if only delimiters
            if (maskDelimiter != maskControl)
            {
                goto HandleMixed;
            }

            ParseDelimiters(maskDelimiter, runningIndex, ref fieldIndex, ref dstField);
            goto ContinueRead;

            HandleMixed:
            if (!TNewline.IsCRLF && controlCount <= 5)
            {
                ParseDelimitersAndLineEndsUnrolled(
                    maskControl,
                    maskControl & ~maskDelimiter,
                    runningIndex,
                    ref Unsafe.Add(ref dstField, fieldIndex)
                );

                fieldIndex += controlCount;
            }
            else
            {
                ParseDelimitersAndLineEnds(
                    maskControl,
                    ref first,
                    delimiter,
                    runningIndex,
                    ref fieldIndex,
                    ref dstField,
                    ref vector
                );
            }
            goto ContinueRead;

            HandleAny:
            uint quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            ParseAny(
                maskControl: maskControl,
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
            hasControl = hasNewline | hasDelimiter;
            hasQuote = Vector256.Equals(vector, quoteVec);
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
            isEOL = TNewline.IsNewline<T, BLSRMaskClear>(delimiter, ref Unsafe.Add(ref first, value), ref mask);

            Unsafe.Add(ref fieldRef, fieldIndex++) = value | (uint)isEOL;
        } while (mask != 0);

        if (offset == Vector256<byte>.Count - 1 && isEOL == FieldFlag.CRLF)
        {
            nextVector &= Vector256<byte>.ZeroFirst;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndLineEndsUnrolled(uint mask, uint maskLF, nuint runningIndex, ref uint dst)
    {
        // here we shift the LF mask so the current bit is a MSB (eol flag)
        uint increment = (uint)runningIndex;

        // TODO: use signed shift on CRLF to get the two top bytes
        // uint flag = (uint)((int)((maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL) >> shift);

        uint tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        uint value = increment + tz;
        uint flag = (maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL;
        Unsafe.Add(ref dst, 0u) = value | flag;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        value = increment + tz;
        flag = (maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL;
        Unsafe.Add(ref dst, 1u) = value | flag;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        value = increment + tz;
        flag = (maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL;
        Unsafe.Add(ref dst, 2u) = value | flag;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        value = increment + tz;
        flag = (maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL;
        Unsafe.Add(ref dst, 3u) = value | flag;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        value = increment + tz;
        flag = (maskLF << (int)(31 - tz)) & (uint)FieldFlag.EOL;
        Unsafe.Add(ref dst, 4u) = value | flag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseAny(
        uint maskControl,
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

        while (maskControl != 0)
        {
            offset = (uint)BitOperations.TrailingZeroCount(maskControl);
            uint maskUpToPos = Bmi1.GetMaskUpToLowestSetBit(maskControl);

            uint value = (uint)runningIndex + offset;
            quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);

            flag = TNewline.IsNewline<T, LeftShiftMaskClear>(
                delimiter,
                ref Unsafe.Add(ref first, value),
                ref maskUpToPos
            );

            maskControl &= ~maskUpToPos;
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
            nextVector &= Vector256<byte>.ZeroFirst;
        }
    }
}

file static class Extensions
{
    extension(Vector256<byte>)
    {
        /// <summary>
        /// Returns a vector with the first byte set to zero and all other bytes set to 0xFF.
        /// </summary>
        public static Vector256<byte> ZeroFirst
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Vector256.Create(-256L, ~0L, ~0L, ~0L).AsByte();
        }
    }
}
