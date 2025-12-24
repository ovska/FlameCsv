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
    protected override int Overscan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count * 3;
    }

    public override int PreferredLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count * 4;
    }

    public override int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count;
    }

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            // ensure the method is trimmed on NAOT
            throw new PlatformNotSupportedException();
        }

#if false
        ReadOnlySpan<T> data = new(start, (int)(end - start));
#endif

        nuint index = (uint)startIndex;
        T* pData = start + index;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination);
        nuint fieldIndex = 0;
        nuint fieldEnd = Math.Max(0, (nuint)destination.Length - (nuint)MaxFieldsPerIteration);

        uint quotesConsumed = 0;
        uint crCarry = 0;

        Vector256<byte> vecDelim = Vector256.Create(byte.CreateTruncating(_delimiter));
        Vector256<byte> vecQuote = Vector256.Create(byte.CreateTruncating(_quote));
        Vector256<byte> vecLF = Vector256.Create((byte)'\n');
        Vector256<byte> vecCR = TCRLF.Value ? Vector256.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load256(pData);

        Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
        Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
        Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);
        Vector256<byte> hasCR = TCRLF.Value ? Vector256.Equals(vector, vecCR) : default;
        Vector256<byte> hasControl = hasLF | hasDelimiter;

        (uint maskControl, uint maskLF, uint maskQuote, uint maskCR) = AsciiVector.MoveMask<TCRLF>(
            hasControl,
            hasLF,
            hasQuote,
            hasCR
        );

#if false // TODO: test on x86
        if (((nuint)pData % (nuint)Vector256<byte>.Count) / (uint)sizeof(T) is nuint remainder and not 0)
        {
            maskControl <<= (int)remainder;
            maskLF <<= (int)remainder;
            maskQuote <<= (int)remainder;

            if (TCRLF.Value)
            {
                maskCR <<= (int)remainder;
            }

            index -= remainder;
            pData -= remainder;
        }
#endif

        Vector256<byte> nextVector = AsciiVector.Load256(pData + (nuint)Vector256<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetch = AsciiVector.Load256(pData + (nuint)(2 * Vector256<byte>.Count));

            Unsafe.SkipInit(out uint shiftedCR); // this can be garbage on LF, it's never used

            vector = nextVector;
            nextVector = prefetch;

            if (TCRLF.Value)
            {
                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;

                if ((maskControl | shiftedCR) == 0)
                {
                    goto SumQuotesAndContinue;
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
                if (maskControl == 0)
                {
                    goto SumQuotesAndContinue;
                }
            }

            uint controlCount;
            QuoteState quoteState = 0;

            if ((maskQuote | quotesConsumed) == 0)
            {
                controlCount = (uint)BitOperations.PopCount(maskControl);
                goto FastPath;
            }

            // go to slow path if multiple quotes
            if (!Bithacks.ZeroOrOneBitsSet(maskQuote))
            {
                goto SlowPath;
            }

            // case 1: no quote in this chunk
            if (maskQuote == 0)
            {
                // quotesConsumed is guaranteed non-zero here, so maskControl is correct too

                // whole chunk is inside quotes
                if ((quotesConsumed & 1) != 0)
                {
                    goto ContinueRead;
                }

                // A string just ended? flush quotes and read the rest as normal
                quoteState = QuoteState.Flush;
                controlCount = (uint)BitOperations.PopCount(maskControl);
                goto FastPath;
            }

            // exactly one quote in this chunk
            maskControl &= Bithacks.FindInverseQuoteMaskSingle(maskQuote, quotesConsumed);

            // after XOR clearing, if no controls survive, just carry quote state forward
            if (maskControl == 0)
            {
                // maskQuote != 0 here, so exactly one quote in this chunk
                ++quotesConsumed;
                goto ContinueRead;
            }

            // surviving controls on one side of the quote
            controlCount = (uint)BitOperations.PopCount(maskControl);

            // thanks to 1-quote guarantee + XOR,
            // controlsBeforeQuote is either 0 (quote before controls) or == maskControl (quote after)
            uint controlsBeforeQuote = (maskQuote - 1u) & maskControl;

            Check.True(controlsBeforeQuote == 0 || controlsBeforeQuote == maskControl);

            if (controlsBeforeQuote == 0)
            {
                // quote is first, flush
                quoteState = QuoteState.Flush;
                quotesConsumed++;
            }
            else if (quotesConsumed != 0)
            {
                // string just ended?
                Check.Equal(quotesConsumed % 2, 0u);
                quoteState = QuoteState.FlushAndCarry;
            }
            else
            {
                // quote is last, flag it for the next iter
                quotesConsumed++;
            }

            FastPath:
            uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

            if (Bithacks.ZeroOrOneBitsSet(maskLF))
            {
                ParseDelimitersAndNewlines(
                    count: controlCount,
                    mask: maskControl,
                    maskLF: maskLF,
                    flag: flag,
                    index: (uint)index,
                    dst: ref Unsafe.Add(ref firstField, fieldIndex)
                );
            }
            else
            {
                ParseControls(
                    index: (uint)index,
                    dst: ref Unsafe.Add(ref firstField, fieldIndex),
                    maskControl: maskControl,
                    maskLF: maskLF,
                    flag: flag
                );
            }

            if (quoteState != 0)
            {
                Unsafe.Add(ref firstField, fieldIndex) |= Bithacks.GetQuoteFlags(quotesConsumed);
                quotesConsumed = (uint)quoteState >> 1;
            }

            fieldIndex += controlCount;
            goto ContinueRead;

            SlowPath:
            maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);
            flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

            ParseAny(
                index: (uint)index,
                firstField: ref firstField,
                fieldIndex: ref fieldIndex,
                quotesConsumed: ref quotesConsumed,
                maskControl: maskControl,
                maskLF: maskLF,
                maskQuote: ref maskQuote,
                flag: flag
            );

            goto SumQuotesAndContinue;

            PathologicalPath:
            if (TCRLF.Value)
            {
                // clear the bits that are inside quotes
                maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);

                CheckDanglingCR(
                    maskControl: ref maskControl,
                    data: ref Unsafe.AsRef<T>(pData),
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quotesConsumed: ref quotesConsumed
                );

                ParsePathological(
                    maskControl: maskControl,
                    maskQuote: ref maskQuote,
                    data: ref Unsafe.AsRef<T>(pData),
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    delimiter: _delimiter,
                    quotesConsumed: ref quotesConsumed
                );
            }

            SumQuotesAndContinue:
            quotesConsumed += (uint)BitOperations.PopCount(maskQuote);

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;
            pData += Vector256<byte>.Count;

            hasLF = Vector256.Equals(vector, vecLF);
            hasCR = TCRLF.Value ? Vector256.Equals(vector, vecCR) : default;
            hasDelimiter = Vector256.Equals(vector, vecDelim);
            hasQuote = Vector256.Equals(vector, vecQuote);
            hasControl = hasLF | hasDelimiter;

            (maskControl, maskLF, maskQuote, maskCR) = AsciiVector.MoveMask<TCRLF>(hasControl, hasLF, hasQuote, hasCR);
        } while (fieldIndex <= fieldEnd && pData < end);

        return (int)fieldIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndNewlines(
        uint count,
        uint mask,
        uint maskLF,
        uint flag,
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
            ref uint dst2 = ref Unsafe.Add(ref dst, UnrollCount);

            m5 &= m5 - 1;

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(m5);
                m5 &= m5 - 1;
                dst2 = index + offset;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (m5 != 0);
        }

        uint lfTz = (uint)BitOperations.TrailingZeroCount(maskLF);
        uint intermediate = index - flag;
        Unsafe.Add(ref dst, lfPos) = intermediate + lfTz;
    }
}

file enum QuoteState
{
    None = 0,
    Flush = 0b1,
    FlushAndCarry = 0b11,
}
