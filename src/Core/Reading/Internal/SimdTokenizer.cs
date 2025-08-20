using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
        get => Vector256<byte>.Count; // e.g. 1 CR and 31 delimiters
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
        nuint index = (uint)startIndex;
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
        uint quotesConsumed = 0;
        uint crCarry = 0;

        Vector256<byte> vecDelim = Vector256.Create(byte.CreateTruncating(_delimiter));
        Vector256<byte> vecQuote = Vector256.Create(byte.CreateTruncating(_quote));
        Vector256<byte> vecLF = Vector256.Create((byte)'\n');
        Vector256<byte> vecCR = TNewline.IsCRLF ? Vector256.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load(ref first, index);

        Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
        Vector256<byte> hasCR = TNewline.IsCRLF ? Vector256.Equals(vector, vecCR) : default;
        Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
        Vector256<byte> hasControl = hasLF | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);

        Vector256<byte> nextVector = AsciiVector.Load(ref first, index + (nuint)Vector256<byte>.Count);

        while (fieldIndex <= fieldEnd && index <= searchSpaceEnd)
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.Load(ref first, index + (nuint)(2 * Vector256<byte>.Count));

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
                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                    goto ContinueRead;
                }

                if (shiftedCR != 0 & shiftedCR != maskLF)
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
                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
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
                    index: (uint)index,
                    dst: ref Unsafe.Add(ref firstField, fieldIndex)
                );

                fieldIndex += controlCount;
                goto ContinueRead;
            }

            SlowPath:
            // clear the bits that are inside quotes
            maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);

            uint flag = Bithacks.GetSubractionFlag<TNewline>(shiftedCR == 0);

            ParseAny(
                index: (uint)index,
                firstField: ref firstField,
                firstQuote: ref firstQuote,
                fieldIndex: ref fieldIndex,
                quotesConsumed: ref quotesConsumed,
                maskControl: maskControl,
                maskLF: maskLF,
                maskQuote: maskQuote,
                flag: flag
            );

            goto ContinueRead;

            PathologicalPath:
            if (TNewline.IsCRLF)
            {
                // clear the bits that are inside quotes
                maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);

                CheckDanglingCR(
                    maskControl: ref maskControl,
                    first: ref first,
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quoteRef: ref firstQuote,
                    quotesConsumed: ref quotesConsumed
                );

                ParsePathological(
                    maskControl: maskControl,
                    maskQuote: maskQuote,
                    first: ref first,
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quoteRef: ref firstQuote,
                    delimiter: _delimiter,
                    quotesConsumed: ref quotesConsumed
                );
            }

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;
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
        uint index,
        ref uint dst
    )
    {
        // on 128bit vectors 3 is optimal; revisit if we change width
        const uint unrollCount = 5;

        uint lfPos = (uint)BitOperations.PopCount(mask & (maskLF - 1));

        Unsafe.Add(ref dst, 0u) = index + (uint)BitOperations.TrailingZeroCount(mask);
        Unsafe.Add(ref dst, 1u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 2u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 3u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 4u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);

        if (count > unrollCount)
        {
            // for some reason this is faster than incrementing a pointer
            ref uint dst2 = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
                dst2 = index + offset;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (mask != 0);
        }

        uint lfTz = (uint)BitOperations.TrailingZeroCount(maskLF);
        Unsafe.Add(ref dst, lfPos) = index + lfTz - Bithacks.GetSubractionFlag<TNewline>(shiftedCR == 0);
    }
}
