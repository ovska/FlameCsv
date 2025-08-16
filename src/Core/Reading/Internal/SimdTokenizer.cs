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
        uint quoteCarry = 0;
        uint crCarry = 0;

        Vector256<byte> vecDelim = AsciiVector.Create(delimiter);
        Vector256<byte> vecQuote = AsciiVector.Create(quote);
        Vector256<byte> vecLF = AsciiVector.Create((byte)'\n');
        Vector256<byte> vecCR = TNewline.IsCRLF ? AsciiVector.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load(ref first, runningIndex);
        Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
        Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
        Vector256<byte> hasControl = hasLF | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
        {
            // TODO: profile Sse.Prefetch0 on multiple machines

            uint maskControl = hasControl.ExtractMostSignificantBits();
            uint maskLF = hasLF.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint shiftedCR);

            if (TNewline.IsCRLF)
            {
                Vector256<byte> hasCR = Vector256.Equals(vector, vecCR);
                uint maskCR = hasCR.ExtractMostSignificantBits();

                vector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;

                if ((maskControl | shiftedCR) == 0)
                {
                    goto Empty;
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
                vector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

                if (maskControl == 0)
                {
                    goto Empty;
                }
            }

            if ((quoteCarry | quotesConsumed | maskQuote) != 0)
            {
                goto SlowPath;
            }

            uint controlCount = (uint)BitOperations.PopCount(maskControl);

            // check if only delimiters
            if (maskLF == 0)
            {
                ParseDelimiters(controlCount, maskControl, runningIndex, ref Unsafe.Add(ref firstField, fieldIndex));
            }
            else
            {
                ParseDelimitersAndLineEndsUnrolled(
                    controlCount,
                    maskControl,
                    maskLF,
                    shiftedCR,
                    runningIndex,
                    ref Unsafe.Add(ref firstField, fieldIndex)
                );
            }

            fieldIndex += controlCount;
            goto ContinueRead;

            SlowPath:
            uint quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
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

            Empty:
            uint quoteCount = (uint)BitOperations.PopCount(maskQuote);
            quotesConsumed += quoteCount;
            Bithacks.ConditionalFlip(ref quoteCarry, quoteCount);
            goto ContinueRead;

            PathologicalPath:
            quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

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

            ContinueRead:
            runningIndex += (nuint)Vector256<byte>.Count;
            hasLF = Vector256.Equals(vector, vecLF);
            hasDelimiter = Vector256.Equals(vector, vecDelim);
            hasControl = hasLF | hasDelimiter;
            hasQuote = Vector256.Equals(vector, vecQuote);
        }

        return (int)fieldIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimiters(uint count, uint mask, nuint runningIndex, ref uint dst)
    {
        // on 128bit vectors 3 is optimal; revisit if we change width
        const uint unrollCount = 5;

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
            dst = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                dst = increment + offset;
                dst = ref Unsafe.Add(ref dst, 1u);
            } while (mask != 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndLineEndsUnrolled(
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

        uint increment = (uint)runningIndex;

        uint flag = Bithacks.GetSubractionFlag<TNewline>(shiftedCR);

        uint tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        uint pos = increment + tz;
        uint magic = Bithacks.ProcessFlag(maskLF, tz, flag);
        Unsafe.Add(ref dst, 0u) = pos - magic;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        pos = increment + tz;
        magic = Bithacks.ProcessFlag(maskLF, tz, flag);
        Unsafe.Add(ref dst, 0u) = pos - magic;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        pos = increment + tz;
        magic = Bithacks.ProcessFlag(maskLF, tz, flag);
        Unsafe.Add(ref dst, 0u) = pos - magic;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        mask &= (mask - 1);
        pos = increment + tz;
        magic = Bithacks.ProcessFlag(maskLF, tz, flag);
        Unsafe.Add(ref dst, 0u) = pos - magic;

        tz = (uint)BitOperations.TrailingZeroCount(mask);
        pos = increment + tz;
        magic = Bithacks.ProcessFlag(maskLF, tz, flag);
        Unsafe.Add(ref dst, 0u) = pos - magic;

        if (count > unrollCount)
        {
            mask &= (mask - 1);

            // for some reason this is faster than incrementing a pointer
            dst = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask);
                mask &= (mask - 1);
                magic = Bithacks.ProcessFlag(maskLF, tz, flag);
                dst = increment + tz + magic;
                dst = ref Unsafe.Add(ref dst, 1u);
            } while (mask != 0);
        }
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
