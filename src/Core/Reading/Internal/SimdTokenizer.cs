using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal sealed class SimdTokenizer<T, TCRLF>(CsvOptions<T> options) : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
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
        Debug.Assert(data.Length <= Field.MaxFieldEnd);

        if ((uint)(data.Length - startIndex) < EndOffset)
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
        Vector256<byte> vecCR = TCRLF.Value ? Vector256.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load256(ref first, index);

        Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
        Vector256<byte> hasCR = TCRLF.Value ? Vector256.Equals(vector, vecCR) : default;
        Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
        Vector256<byte> hasControl = hasLF | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);

        uint maskCR = TCRLF.Value ? hasCR.MoveMask() : 0;
        uint maskControl = hasControl.MoveMask();
        uint maskLF = hasLF.MoveMask();
        uint maskQuote = hasQuote.MoveMask();

        Vector256<byte> nextVector = AsciiVector.Load256(ref first, index + (nuint)Vector256<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.Load256(ref first, index + (nuint)(2 * Vector256<byte>.Count));

            Unsafe.SkipInit(out uint shiftedCR); // this can be garbage on LF, it's never used

            if (TCRLF.Value)
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

                if (Bithacks.IsDisjointCR(maskLF, shiftedCR))
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

            uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

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
            if (TCRLF.Value)
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
            hasCR = TCRLF.Value ? Vector256.Equals(vector, vecCR) : default;
            hasDelimiter = Vector256.Equals(vector, vecDelim);
            hasQuote = Vector256.Equals(vector, vecQuote);
            hasControl = hasLF | hasDelimiter;

            maskCR = TCRLF.Value ? hasCR.MoveMask() : 0;
            maskControl = hasControl.MoveMask();
            maskLF = hasLF.MoveMask();
            maskQuote = hasQuote.MoveMask();
        } while (fieldIndex <= fieldEnd && index <= searchSpaceEnd);

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
        // even on arm64, this pattern is quite a bit faster than using rbit once and clz after
        // perhaps due to less instructions (bit clearing takes much more on ARM)

        const uint UnrollCount = 5; // optimal for 256bit, 3 for 128bit

        uint lfPos = (uint)BitOperations.PopCount(mask & (maskLF - 1));

        // reusing locals here causes regressions on x86
        uint m2 = mask & mask - 1;
        Unsafe.Add(ref dst, 0u) = index + (uint)BitOperations.TrailingZeroCount(mask);
        uint m3 = m2 & m2 - 1;
        Unsafe.Add(ref dst, 1u) = index + (uint)BitOperations.TrailingZeroCount(m2);
        uint m4 = m3 & m3 - 1;
        Unsafe.Add(ref dst, 2u) = index + (uint)BitOperations.TrailingZeroCount(m3);
        uint m5 = m4 & m4 - 1;
        Unsafe.Add(ref dst, 3u) = index + (uint)BitOperations.TrailingZeroCount(m4);
        Unsafe.Add(ref dst, 4u) = index + (uint)BitOperations.TrailingZeroCount(m5);

        if (count > UnrollCount)
        {
            // for some reason this is faster than incrementing a pointer
            ref uint dst2 = ref Unsafe.Add(ref dst, UnrollCount);

            m5 &= m5 - 1;

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(m5);
                dst2 = index + offset;
                m5 &= m5 - 1;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (m5 != 0);
        }

        uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;
        uint lfTz = (uint)BitOperations.TrailingZeroCount(maskLF);
        uint intermediate = index - flag;
        Unsafe.Add(ref dst, lfPos) = intermediate + lfTz;
    }
}
