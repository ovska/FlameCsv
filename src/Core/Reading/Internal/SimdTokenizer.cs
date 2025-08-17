using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
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
        get => Vector256<byte>.Count * 3;
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

        scoped ref uint firstField = ref MemoryMarshal.GetReference(buffer.Fields);
        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
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
        uint crCarry = 0;

        Vector256<byte> vecDelim = AsciiVector.Create(delimiter);
        Vector256<byte> vecQuote = AsciiVector.Create(quote);
        Vector256<byte> vecLF = AsciiVector.Create((byte)'\n');
        Vector256<byte> vecCR = TNewline.IsCRLF ? AsciiVector.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load(ref first, runningIndex);

        Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
        Vector256<byte> hasCR = TNewline.IsCRLF ? Vector256.Equals(vector, vecCR) : default;
        Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
        Vector256<byte> hasControl = hasLF | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);

        Vector256<byte> nextVector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.Load(
                ref first,
                runningIndex + (nuint)(2 * Vector256<byte>.Count)
            );

            uint maskCR = TNewline.IsCRLF ? hasCR.ExtractMostSignificantBits() : 0;
            uint maskControl = hasControl.ExtractMostSignificantBits();
            uint maskLF = hasLF.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint shiftedCR);

            if (TNewline.IsCRLF)
            {
                vector = nextVector;
                nextVector = prefetchVector;

                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;

                if ((maskControl | shiftedCR) == 0)
                {
                    uint quoteCount = (uint)BitOperations.PopCount(maskQuote);
                    quotesConsumed += quoteCount;
                    goto ContinueRead;
                }

                if ((shiftedCR & (shiftedCR ^ maskLF)) != 0)
                {
                    // maskControl doesn't contain CR by default, add it so we can find lone CR's
                    maskControl |= maskCR;
                    goto PathologicalPath;
                }
            }
            else
            {
                vector = nextVector;
                nextVector = prefetchVector;

                if (maskControl == 0)
                {
                    uint quoteCount = (uint)BitOperations.PopCount(maskQuote);
                    quotesConsumed += quoteCount;
                    goto ContinueRead;
                }
            }

            uint controlCount = (uint)BitOperations.PopCount(maskControl);

            if ((quotesConsumed | maskQuote) != 0)
            {
                goto SlowPath;
            }

            if (Bithacks.ZeroOrOneBitsSet(maskLF))
            {
                ParseDelimitersAndNewlines(
                    count: controlCount,
                    mask: maskControl,
                    maskLF: maskLF,
                    shiftedCR: shiftedCR,
                    runningIndex: runningIndex,
                    dst: ref Unsafe.Add(ref firstField, fieldIndex)
                );

                fieldIndex += controlCount;
                goto ContinueRead;
            }

            SlowPath:
            uint quoteXOR = Bithacks.ComputeQuoteMask(maskQuote) ^ (0u - (quotesConsumed & 1));
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            uint flag = Bithacks.GetSubractionFlag<TNewline>(shiftedCR);

            while (maskControl != 0)
            {
                uint tz = (uint)BitOperations.TrailingZeroCount(maskControl);
                uint maskUpToPos = Bithacks.GetMaskUpToLowestSetBit(maskControl, tz);

                uint eolFlag = Bithacks.ProcessFlag(maskLF, tz, flag);
                uint pos = (uint)runningIndex + tz;
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);

                // consume masks
                maskControl &= ~maskUpToPos;
                maskQuote &= ~maskUpToPos;

                Field.SaturateTo7Bits(ref quotesConsumed);

                Unsafe.Add(ref firstField, fieldIndex) = pos - eolFlag;
                Unsafe.Add(ref firstQuote, fieldIndex) = (byte)quotesConsumed;

                quotesConsumed = 0;
                fieldIndex++;
            }

            quotesConsumed += (uint)BitOperations.PopCount(maskQuote);

            goto ContinueRead;

            PathologicalPath:
            if (TNewline.IsCRLF)
            {
                uint quoteXOR2 = Bithacks.ComputeQuoteMask(maskQuote) ^ (0u - (quotesConsumed & 1));
                maskControl &= ~quoteXOR2; // clear the bits that are inside quotes

                ParsePathological(
                    maskControl: maskControl,
                    maskQuote: maskQuote,
                    first: ref first,
                    runningIndex: runningIndex,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quoteRef: ref firstQuote,
                    delimiter: delimiter,
                    quotesConsumed: ref quotesConsumed,
                    nextVector: ref vector
                );
            }

            ContinueRead:
            runningIndex += (nuint)Vector256<byte>.Count;
            hasLF = Vector256.Equals(vector, vecLF);
            hasCR = TNewline.IsCRLF ? Vector256.Equals(vector, vecCR) : default;
            hasDelimiter = Vector256.Equals(vector, vecDelim);
            hasQuote = Vector256.Equals(vector, vecQuote);
            hasControl = hasLF | hasDelimiter;
        }

        return (int)fieldIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndNewlines(
        uint count,
        uint mask,
        uint maskLF,
        uint shiftedCR,
        nuint runningIndex,
        ref uint dst
    )
    {
        // on 128bit vectors 3 is optimal; revisit if we change width
        const uint unrollCount = 5;

        uint lfPos = (uint)BitOperations.PopCount(mask & (maskLF - 1));

        uint increment = (uint)runningIndex;

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
            ref uint dst2 = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                dst2 = increment + offset;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (mask != 0);
        }

        Unsafe.Add(ref dst, lfPos) =
            (uint)BitOperations.TrailingZeroCount(maskLF) | Bithacks.GetSubractionFlag<TNewline>(shiftedCR);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParsePathological(
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
