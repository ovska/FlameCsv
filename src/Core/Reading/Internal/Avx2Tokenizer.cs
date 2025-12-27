using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx2Tokenizer
{
    public static bool IsSupported =>
        Avx2.IsSupported && RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64;
}

[SkipLocalsInit]
internal sealed class Avx2Tokenizer<T, TCRLF, TQuote> : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
    where TQuote : struct, IConstant
{
    protected override int Overscan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count * 3;
    }

    public override int PreferredLength => Vector256<byte>.Count * 4;

    public override int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count;
    }

    private readonly T _quote;
    private readonly T _delimiter;

    public Avx2Tokenizer(CsvOptions<T> options)
    {
        Check.Equal(TCRLF.Value, options.Newline.IsCRLF(), "CRLF constant must match newline option.");
        Check.Equal(TQuote.Value, options.Quote.HasValue, "Quote constant must match presence of quote char.");
        _quote = T.CreateTruncating(options.Quote.GetValueOrDefault());
        _delimiter = T.CreateTruncating(options.Delimiter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end)
    {
        if (!Avx2.IsSupported || RuntimeInformation.ProcessArchitecture is not (Architecture.X86 or Architecture.X64))
        {
            // ensure the method is trimmed on NAOT
            throw new PlatformNotSupportedException();
        }

#if false
        ReadOnlySpan<T> data = new(start, (int)(end - start));
#endif

        _ = CompressionTables.BlendMask; // ensure static ctor is run

        nuint index = (nuint)startIndex;
        T* pData = start + index;

        nuint fieldEnd = (nuint)destination.Length - (nuint)MaxFieldsPerIteration;
        nuint fieldIndex = 0;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination);

        Vector256<byte> vecDelim = Vector256.Create(byte.CreateTruncating(_delimiter));
        Vector256<byte> vecQuote = TQuote.Value ? Vector256.Create(byte.CreateTruncating(_quote)) : default;
        Vector256<byte> vecLF = Vector256.Create((byte)'\n');
        Vector256<byte> vecCR = TCRLF.Value ? Vector256.Create((byte)'\r') : default;
        Vector256<long> addCnst = Vector256.Create(0L, 0x0808080808080808, 0x1010101010101010, 0x1818181818181818);

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector256<uint> iterationLength = Vector256.Create((uint)Vector256<byte>.Count);

        uint quotesConsumed = 0;
        uint crCarry = 0;

        Vector256<byte> vector = AsciiVector.Load256(pData);
        Vector256<uint> indexVector;

        nint remainder = ((nint)pData % Vector256<byte>.Count) / sizeof(T);

        if (remainder != 0)
        {
            vector = AsciiVector.ShiftItemsRight(vector, (int)remainder);
            indexVector = Vector256.Create((uint)index - (uint)remainder);
            index -= (nuint)remainder;
            pData -= remainder;
        }
        else
        {
            indexVector = Vector256.Create((uint)index);
        }

        Vector256<byte> nextVector = AsciiVector.Load256(pData + Vector256<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.Load256(pData + (2 * Vector256<byte>.Count));

            Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
            Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
            Vector256<byte> hasQuote = TQuote.Value ? Vector256.Equals(vector, vecQuote) : Vector256<byte>.Zero;
            Vector256<byte> hasControl = hasLF | hasDelimiter;

            uint maskControl = hasControl.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint maskQuote);
            Unsafe.SkipInit(out uint maskLF); // calculated only on-demand for LF newlines
            Unsafe.SkipInit(out uint shiftedCR); // never used on LF newlines

            if (TQuote.Value)
            {
                maskQuote = hasQuote.ExtractMostSignificantBits();
            }

            if (TCRLF.Value)
            {
                Vector256<byte> hasCR = Vector256.Equals(vector, vecCR);
                uint maskCR = hasCR.ExtractMostSignificantBits();
                maskLF = hasLF.ExtractMostSignificantBits();

                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;

                // Load next vector while waiting for the movemasks
                vector = nextVector;
                nextVector = prefetchVector;

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
                vector = nextVector;
                nextVector = prefetchVector;

                if (maskControl == 0)
                {
                    goto SumQuotesAndContinue;
                }
            }

            uint matchCount = (uint)BitOperations.PopCount(maskControl);

            // rare cases: quotes, or too many matches to fit in the compress path
            if (TQuote.Value && (quotesConsumed | maskQuote) != 0)
            {
                goto SlowPath;
            }

            if (matchCount > (uint)Vector256<int>.Count)
            {
                if (!TCRLF.Value)
                {
                    maskLF = hasLF.ExtractMostSignificantBits();
                }

                uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;
                ParseControls((uint)index, ref Unsafe.Add(ref firstField, fieldIndex), maskControl, maskLF, flag);
                fieldIndex += matchCount;
                goto ContinueRead;
            }

            // build a mask to remove extra bits caused by sign-extension
            Vector256<int> fixup = TCRLF.Value
                ? Vector256.Create(fixupScalar | ((shiftedCR != 0).ToByte() << 30))
                : Vector256.Create(fixupScalar);

            uint mask1 = ~maskControl;

            ref ulong thinEpi8 = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);

            uint mask2 = mask1 >> 8;
            uint mask3 = mask1 >> 16;
            uint mask4 = mask1 >> 24;

            // load the shuffle mask from the set bits
            Vector256<ulong> shufmask = Vector256.Create(
                Unsafe.Add(ref thinEpi8, (byte)mask1),
                Unsafe.Add(ref thinEpi8, (byte)mask2),
                Unsafe.Add(ref thinEpi8, (byte)mask3),
                Unsafe.Add(ref thinEpi8, (byte)mask4)
            );

            ref byte popCounts = ref Unsafe.AsRef(in CompressionTables.PopCountMult2[0]);

            // add the lane offset constant to the shuffle mask
            Vector256<byte> shufmaskBytes = shufmask.AsByte() + addCnst.AsByte();

            // get the offset for the upper lane
            uint lowerCountOffset = (uint)BitOperations.PopCount(maskControl & 0xFFFF);

            byte pop1 = Unsafe.Add(ref popCounts, (byte)mask1);
            byte pop3 = Unsafe.Add(ref popCounts, (byte)mask3);

            // tag the indices with the MSB if they are newlines (shift 0xFF to bit 7)
            Vector256<byte> taggedIndices = (hasLF << 7) | Vector256<byte>.Indices;

            // jit optimizes this add to a constant address
            ref byte shuffleCombine = ref Unsafe.Add(ref Unsafe.AsRef(in CompressionTables.ShuffleCombine[0]), 16);

            // Load the 128-bit masks from pshufb_combine_table
            // note that the upper lane is offset to the correct position already, e.g.
            // < 1 2 0 0 0 ... 3 4 5 ... > will leave 2 items empty on the upper lane
            Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, pop1 * 8u);
            Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, (pop3 * 8u) - lowerCountOffset);

            // shuffle the indexes to their correct positions
            Vector256<byte> pruned = Avx2.Shuffle(taggedIndices, shufmaskBytes);

            // get the blend mask to combine lower and upper lanes
            Vector128<byte> blend = CompressionTables.LoadBlendMask(lowerCountOffset);

            // arrange the results into two lanes
            Vector128<byte> lower = Ssse3.Shuffle(pruned.GetLower(), combine0);
            Vector128<byte> upper = Ssse3.Shuffle(pruned.GetUpper(), combine1);

            // blend the higher and lower lanes to their final positions
            Vector128<byte> combined = Sse41.BlendVariable(lower, upper, blend);

            // sign-extend to int32 to keep the CR/LF tags
            Vector256<int> taggedIndexVector = Avx2.ConvertToVector256Int32(combined.AsSByte());

            // clear extra sign-extended bits between the EOL flags and indices
            Vector256<uint> fixedTaggedVector = (taggedIndexVector & fixup).AsUInt32();

            Unsafe.SkipInit(out Vector256<uint> crlfShift);

            if (TCRLF.Value)
            {
                // this must be done on the fixed value, otherwise LF's read with CRLF mode get messed up
                crlfShift = ((fixedTaggedVector.AsUInt32() >> 30) & Vector256<uint>.One);
            }

            // add the base offset to the "tzcnts"
            Vector256<uint> result = fixedTaggedVector + indexVector;

            if (TCRLF.Value)
            {
                // subtract 1 from CRLF matches
                result -= crlfShift;
            }

            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;
            pData += Vector256<byte>.Count;
            indexVector += iterationLength;
            continue;

            SlowPath:
            Check.True(TQuote.Value, "SlowPath should only be taken when quotes are enabled.");
            if (TQuote.Value)
            {
                if (!TCRLF.Value)
                {
                    maskLF = hasLF.ExtractMostSignificantBits();
                }

                // clear the bits that are inside quotes
                maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);

                uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

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
            }

            goto SumQuotesAndContinue;

            PathologicalPath:
            if (TCRLF.Value)
            {
                if (TQuote.Value)
                {
                    // clear the bits that are inside quotes
                    maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);
                }
                else
                {
                    maskQuote = 0;
                }

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
            if (TQuote.Value)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
            }

            goto ContinueRead;
        } while (fieldIndex <= fieldEnd && pData <= end);

        return (int)fieldIndex;
    }
}
