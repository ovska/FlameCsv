using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
using static System.Runtime.Intrinsics.Arm.AdvSimd;
using Mask = ulong;
using V128 = System.Runtime.Intrinsics.Vector128<byte>;
using V512 = System.Runtime.Intrinsics.Vector512<byte>;
using V64 = System.Runtime.Intrinsics.Vector64<byte>;

namespace FlameCsv.Reading.Internal;

internal static class ArmTokenizer
{
    public static bool IsSupported => Arm64.IsSupported;
}

[SkipLocalsInit]
internal sealed class ArmTokenizer<T, TCRLF, TQuote> : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
    where TQuote : struct, IConstant
{
    protected override int Overscan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => V512.Count * 2;
    }

    public override int PreferredLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => V512.Count * 2;
    }

    public override int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => V512.Count;
    }

    private readonly byte _quote;
    private readonly byte _delimiter;

    public ArmTokenizer(CsvOptions<T> options)
    {
        Check.Equal(TCRLF.Value, options.Newline.IsCRLF(), "CRLF constant must match newline option.");
        Check.Equal(TQuote.Value, options.Quote.HasValue, "Quote constant must match presence of quote char.");
        _quote = (byte)options.Quote.GetValueOrDefault();
        _delimiter = (byte)options.Delimiter;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end)
    {
        if (!Arm64.IsSupported)
        {
            // ensure the method is trimmed on NAOT
            throw new PlatformNotSupportedException();
        }

#if false
        ReadOnlySpan<T> data = new(start, (int)(end - start));
#endif

        nuint index = (uint)startIndex;
        T* pData = start + index;

        scoped ref uint dst = ref MemoryMarshal.GetReference(destination);
        scoped ref readonly uint fieldEnd = ref Unsafe.Add(ref dst, destination.Length - MaxFieldsPerIteration);

        uint quotesConsumed = 0;
        Mask crCarry = 0;

        V128 vecDelim = Vector128.Create(byte.CreateTruncating(_delimiter));
        V128 vecQuote = TQuote.Value ? Vector128.Create(byte.CreateTruncating(_quote)) : default;
        V128 vecLF = Vector128.Create((byte)'\n');
        V128 vecCR = TCRLF.Value ? Vector128.Create((byte)'\r') : default;

        V512 vector = AsciiVector.LoadZipped512(pData);
        V512 nextVector;

        do
        {
            Mask maskControl,
                maskLF,
                maskCR,
                maskQuote;

            {
                V512 hasDelimiter = V.Cmp(vector, vecDelim);
                V512 hasLF = V.Cmp(vector, vecLF);
                V512 hasCR = TCRLF.Value ? V.Cmp(vector, vecCR) : default;
                V512 hasQuote = TQuote.Value ? V.Cmp(vector, vecQuote) : default;

                (Mask maskDelimiter, maskLF, maskCR, maskQuote) = V.MoveMask4x<TCRLF, TQuote>(
                    hasDelimiter,
                    hasLF,
                    hasCR,
                    hasQuote
                );
                maskControl = maskDelimiter | maskLF;

                nextVector = AsciiVector.LoadZipped512(pData + (nuint)V512.Count);
            }

            Unsafe.SkipInit(out Mask shiftedCR); // this can be garbage on LF, it's never used

            if (TCRLF.Value)
            {
                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> ((sizeof(Mask) * 8) - 1);

                if ((maskControl | shiftedCR) == 0)
                {
                    goto SumQuotesAndContinue;
                }

                if (Bithacks.IsDisjointCR(maskLF, shiftedCR))
                {
                    return -1;
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

            if (!TQuote.Value)
            {
                controlCount = (uint)BitOperations.PopCount(maskControl);
            }
            else
            {
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
                Mask controlsBeforeQuote = (maskQuote - 1u) & maskControl;

                Check.True(controlsBeforeQuote == 0 || controlsBeforeQuote == maskControl);

                if (controlsBeforeQuote == 0)
                {
                    // quote is first, flush
                    quoteState = QuoteState.Flush;
                    quotesConsumed++;
                }
                else if (quotesConsumed != 0)
                {
                    // string just ended? very rare
                    Check.Equal(quotesConsumed % 2, 0u);
                    quoteState = QuoteState.FlushAndCarry;
                }
                else
                {
                    // quote is last, flag it for the next iter
                    quotesConsumed++;
                }
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
                    dst: ref dst
                );
            }
            else
            {
                ParseControls(index: (uint)index, dst: ref dst, maskControl: maskControl, maskLF: maskLF, flag: flag);
            }

            if (TQuote.Value && quoteState != 0)
            {
                dst |= Bithacks.GetQuoteFlags(quotesConsumed);
                quotesConsumed = (uint)quoteState >> 1;
            }

            dst = ref Unsafe.Add(ref dst, controlCount);
            goto ContinueRead;

            SlowPath:
            Check.True(TQuote.Value, "SlowPath should only be taken when quotes are enabled.");
            if (TQuote.Value)
            {
                maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);
                flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

                dst = ref ParseAny(
                    index: (uint)index,
                    dst: ref dst,
                    quotesConsumed: ref quotesConsumed,
                    maskControl: maskControl,
                    maskLF: maskLF,
                    maskQuote: ref maskQuote,
                    flag: flag
                );
            }

            SumQuotesAndContinue:
            if (TQuote.Value)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
            }

            ContinueRead:
            vector = nextVector;
            index += (nuint)V512.Count;
            pData += V512.Count;
        } while (pData < end && Unsafe.IsAddressLessThanOrEqualTo(in dst, in fieldEnd));

        return Unsafe.ElementOffset(in MemoryMarshal.GetReference(destination), in dst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndNewlines(
        uint count,
        Mask mask,
        Mask maskLF,
        uint flag,
        uint index,
        ref uint dst
    )
    {
        // even on arm64, this pattern is quite a bit faster than using rbit once and clz after
        // perhaps due to less instructions (bit clearing takes much more on ARM)

        const uint UnrollCount = 8; // optimal: 256=5, 128=3, 512=8

        uint lfPos = (uint)BitOperations.PopCount(mask & (maskLF - 1));

        Mask m2 = mask & mask - 1;
        Unsafe.Add(ref dst, 0u) = index + (uint)BitOperations.TrailingZeroCount(mask);
        Mask m3 = m2 & m2 - 1;
        Unsafe.Add(ref dst, 1u) = index + (uint)BitOperations.TrailingZeroCount(m2);
        Mask m4 = m3 & m3 - 1;
        Unsafe.Add(ref dst, 2u) = index + (uint)BitOperations.TrailingZeroCount(m3);
        Mask m5 = m4 & m4 - 1;
        Unsafe.Add(ref dst, 3u) = index + (uint)BitOperations.TrailingZeroCount(m4);
        Mask m6 = m5 & m5 - 1;
        Unsafe.Add(ref dst, 4u) = index + (uint)BitOperations.TrailingZeroCount(m5);
        Mask m7 = m6 & m6 - 1;
        Unsafe.Add(ref dst, 5u) = index + (uint)BitOperations.TrailingZeroCount(m6);
        Mask m8 = m7 & m7 - 1;
        Unsafe.Add(ref dst, 6u) = index + (uint)BitOperations.TrailingZeroCount(m7);
        Unsafe.Add(ref dst, 7u) = index + (uint)BitOperations.TrailingZeroCount(m8);

        if (count > UnrollCount)
        {
            ref uint dst2 = ref Unsafe.Add(ref dst, UnrollCount);

            m8 &= m8 - 1;

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(m8);
                m8 &= m8 - 1;
                dst2 = index + offset;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (m8 != 0);
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

file static class V
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static V512 Cmp(V512 v, V128 needle)
    {
        return Vector512.Create(
            Vector256.Create(
                CompareEqual(v.GetLower().GetLower(), needle),
                CompareEqual(v.GetLower().GetUpper(), needle)
            ),
            Vector256.Create(
                CompareEqual(v.GetUpper().GetLower(), needle),
                CompareEqual(v.GetUpper().GetUpper(), needle)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Mask MoveMask(V512 v)
    {
        V128 t0 = ShiftRightAndInsert(v.GetLower().GetUpper(), v.GetLower().GetLower(), 1);
        V128 t1 = ShiftRightAndInsert(v.GetUpper().GetUpper(), v.GetUpper().GetLower(), 1);
        V128 t2 = ShiftRightAndInsert(t1, t0, 2);
        V128 t3 = ShiftRightAndInsert(t2, t2, 4);
        V64 t4 = ShiftRightLogicalNarrowingLower(t3.AsUInt16(), 4);
        return t4.AsUInt64().GetElement(0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Mask, Mask, Mask, Mask) MoveMask4x<T2, T3>(V512 v0, V512 v1, V512 v2, V512 v3)
        where T2 : struct, IConstant
        where T3 : struct, IConstant
    {
        V128 a0 = ShiftRightAndInsert(v0.GetLower().GetUpper(), v0.GetLower().GetLower(), 1);
        V128 a1 = ShiftRightAndInsert(v1.GetLower().GetUpper(), v1.GetLower().GetLower(), 1);
        V128 a2 = T2.Value ? ShiftRightAndInsert(v2.GetLower().GetUpper(), v2.GetLower().GetLower(), 1) : default;
        V128 a3 = T3.Value ? ShiftRightAndInsert(v3.GetLower().GetUpper(), v3.GetLower().GetLower(), 1) : default;
        V128 b0 = ShiftRightAndInsert(v0.GetUpper().GetUpper(), v0.GetUpper().GetLower(), 1);
        V128 b1 = ShiftRightAndInsert(v1.GetUpper().GetUpper(), v1.GetUpper().GetLower(), 1);
        V128 b2 = T2.Value ? ShiftRightAndInsert(v2.GetUpper().GetUpper(), v2.GetUpper().GetLower(), 1) : default;
        V128 b3 = T3.Value ? ShiftRightAndInsert(v3.GetUpper().GetUpper(), v3.GetUpper().GetLower(), 1) : default;
        V128 c0 = ShiftRightAndInsert(b0, a0, 2);
        V128 c1 = ShiftRightAndInsert(b1, a1, 2);
        V128 c2 = T2.Value ? ShiftRightAndInsert(b2, a2, 2) : default;
        V128 c3 = T3.Value ? ShiftRightAndInsert(b3, a3, 2) : default;
        V128 d0 = ShiftRightAndInsert(c0, c0, 4);
        V128 d1 = ShiftRightAndInsert(c1, c1, 4);
        V128 d2 = T2.Value ? ShiftRightAndInsert(c2, c2, 4) : default;
        V128 d3 = T3.Value ? ShiftRightAndInsert(c3, c3, 4) : default;
        V64 e0 = ShiftRightLogicalNarrowingLower(d0.AsUInt16(), 4);
        V64 e1 = ShiftRightLogicalNarrowingLower(d1.AsUInt16(), 4);
        V64 e2 = T2.Value ? ShiftRightLogicalNarrowingLower(d2.AsUInt16(), 4) : default;
        V64 e3 = T3.Value ? ShiftRightLogicalNarrowingLower(d3.AsUInt16(), 4) : default;

        return (
            e0.AsUInt64().GetElement(0),
            e1.AsUInt64().GetElement(0),
            T2.Value ? e2.AsUInt64().GetElement(0) : 0,
            T3.Value ? e3.AsUInt64().GetElement(0) : 0
        );
    }
}
