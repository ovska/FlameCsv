using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class ArmTokenizer
{
    public static bool IsSupported => ArmBase.IsSupported && AdvSimd.Arm64.IsSupported;
}

[SkipLocalsInit]
internal sealed class ArmTokenizer<T, TCRLF>(CsvOptions<T> options) : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
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
    public override int Tokenize(FieldBuffer destination, int startIndex, ReadOnlySpan<T> data)
    {
        Debug.Assert(data.Length <= Field.MaxFieldEnd);

        if ((uint)(data.Length - startIndex) < EndOffset || destination.Fields.Length < MaxFieldsPerIteration)
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - (nuint)EndOffset;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination.Fields);
        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(destination.Quotes);
        nuint fieldIndex = 0;
        nuint fieldEnd = Math.Max(0, (nuint)destination.Fields.Length - (nuint)MaxFieldsPerIteration);

        // ensure the worst case doesn't read past the end (e.g. data ends in Vector.Count delimiters)
        // we do this so there are no bounds checks in the loops
        Debug.Assert(searchSpaceEnd < (nuint)data.Length);
        Debug.Assert(destination.Fields.Length >= Vector256<byte>.Count);
        Debug.Assert(destination.Quotes.Length >= Vector256<byte>.Count);

        // load the constants into registers
        uint quotesConsumed = 0;
        uint crCarry = 0;
        ref ulong thinEpi8 = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);
        ref byte popcntLut = ref Unsafe.AsRef(in CompressionTables.PopCount[0]);

        Vector64<byte> maxMatchesPerLane = Vector64.Create((byte)Vector128<int>.Count);
        Vector128<long> upperLaneOffset = Vector128.Create(0L, 0x0808080808080808L);
        Vector128<byte> iotaLo = Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
        Vector128<byte> iotaHi = Vector128.Create((byte)16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31);

        const uint fixupScalar = 0b10000000000000000000000000111111;
        Vector128<uint> fixup = TCRLF.Value ? default : Vector128.Create(fixupScalar);

        Vector128<byte> vecDelim = Vector128.Create(byte.CreateTruncating(_delimiter));
        Vector128<byte> vecQuote = Vector128.Create(byte.CreateTruncating(_quote));
        Vector128<byte> vecLF = Vector128.Create((byte)'\n');
        Vector128<byte> vecCR = TCRLF.Value ? Vector128.Create((byte)'\r') : default;

        Vector256<byte> vector = AsciiVector.Load256(ref first, index);

        Vector256<byte> hasLF = Vector256.Equals128(vector, vecLF);
        Vector256<byte> hasCR = TCRLF.Value ? Vector256.Equals128(vector, vecCR) : default;
        Vector256<byte> hasDelimiter = Vector256.Equals128(vector, vecDelim);
        Vector256<byte> hasControl = hasLF | hasDelimiter;
        Vector256<byte> hasQuote = Vector256.Equals128(vector, vecQuote);

        (uint maskControl, uint maskLF, uint maskQuote, uint maskCR) = AsciiVector.MoveMask<TCRLF>(
            hasControl,
            hasLF,
            hasQuote,
            hasCR
        );

        Vector256<byte> nextVector = AsciiVector.Load256(ref first, index + (nuint)Vector256<byte>.Count);

        while (fieldIndex <= fieldEnd && index <= searchSpaceEnd)
        {
            Unsafe.SkipInit(out uint shiftedCR); // this can be garbage on LF, it's never used

            if (TCRLF.Value)
            {
                vector = nextVector;
                nextVector = AsciiVector.Load256(ref first, index + (nuint)(Vector256<byte>.Count));

                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;

                if ((maskControl | shiftedCR) == 0)
                {
                    goto SumQuotes;
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
                nextVector = AsciiVector.Load256(ref first, index + (nuint)(Vector256<byte>.Count * 2));

                if (maskControl == 0)
                {
                    goto SumQuotes;
                }
            }

            Vector64<byte> aggregated = AdvSimd.CompareGreaterThan(
                AdvSimd.PopCount(Vector64.CreateScalar(maskControl).AsByte()),
                maxMatchesPerLane
            );
            long anyLaneOver4 = aggregated.AsInt64().ToScalar();

            if ((quotesConsumed | maskQuote) != 0 || anyLaneOver4 != 0)
            {
                goto SlowPath;
            }

            ref uint dst = ref Unsafe.Add(ref firstField, fieldIndex);

            uint mask0 = ~maskControl;
            uint mask1 = mask0 >> 8;
            uint mask2 = mask0 >> 16;
            uint mask3 = mask0 >> 24;

            Vector128<ulong> shufmask01 = Vector128.Create(
                Unsafe.Add(ref thinEpi8, (byte)mask0),
                Unsafe.Add(ref thinEpi8, (byte)mask1)
            );
            Vector128<ulong> shufmask23 = Vector128.Create(
                Unsafe.Add(ref thinEpi8, (byte)mask2),
                Unsafe.Add(ref thinEpi8, (byte)mask3)
            );

            int imm8 = TCRLF.Value ? 6 : 7;
            Vector128<byte> tagged0 = (hasLF.GetLower() << imm8) | iotaLo;
            Vector128<byte> tagged1 = (hasLF.GetUpper() << imm8) | iotaHi;

            Vector128<byte> shufMaskBytes01 = shufmask01.AsByte() + upperLaneOffset.AsByte();
            Vector128<byte> shufMaskBytes23 = shufmask23.AsByte() + upperLaneOffset.AsByte();

            Vector128<byte> shuf01 = AdvSimd.Arm64.VectorTableLookup(tagged0, shufMaskBytes01);
            Vector128<byte> shuf23 = AdvSimd.Arm64.VectorTableLookup(tagged1, shufMaskBytes23);

            if (TCRLF.Value)
            {
                fixup = Vector128.Create(fixupScalar | (uint)Unsafe.BitCast<bool, byte>(shiftedCR != 0) << 30);
            }

            Vector128<short> wide0 = AdvSimd.SignExtendWideningLower(shuf01.GetLower().AsSByte());
            Vector128<short> wide1 = AdvSimd.SignExtendWideningUpper(shuf01.AsSByte());
            Vector128<short> wide2 = AdvSimd.SignExtendWideningLower(shuf23.GetLower().AsSByte());
            Vector128<short> wide3 = AdvSimd.SignExtendWideningUpper(shuf23.AsSByte());

            Vector128<uint> wider0 = AdvSimd.SignExtendWideningLower(wide0.GetLower()).AsUInt32();
            Vector128<uint> wider1 = AdvSimd.SignExtendWideningLower(wide1.GetLower()).AsUInt32();
            Vector128<uint> wider2 = AdvSimd.SignExtendWideningLower(wide2.GetLower()).AsUInt32();
            Vector128<uint> wider3 = AdvSimd.SignExtendWideningLower(wide3.GetLower()).AsUInt32();

            Vector128<uint> indexVector = Vector128.Create((uint)index);

            Vector128<uint> fixed0 = wider0 & fixup;
            Vector128<uint> fixed1 = wider1 & fixup;
            Vector128<uint> fixed2 = wider2 & fixup;
            Vector128<uint> fixed3 = wider3 & fixup;

            uint pop0 = Unsafe.Add(ref popcntLut, (byte)~mask0);

            Vector128<uint> final0 = fixed0 + indexVector;
            Vector128<uint> final1 = fixed1 + indexVector;
            Vector128<uint> final2 = fixed2 + indexVector;
            Vector128<uint> final3 = fixed3 + indexVector;

            if (TCRLF.Value)
            {
                // subtract 1 from CRLF matches
                final0 -= (fixed0 << 1 >> 31);
                final1 -= (fixed1 << 1 >> 31);
                final2 -= (fixed2 << 1 >> 31);
                final3 -= (fixed3 << 1 >> 31);
            }

            uint pop01 = pop0 + Unsafe.Add(ref popcntLut, (byte)~mask1);

            final0.StoreUnsafe(ref dst, 0);
            final1.StoreUnsafe(ref dst, pop0);

            uint pop012 = pop01 + Unsafe.Add(ref popcntLut, (byte)~mask2);

            final2.StoreUnsafe(ref dst, pop01);
            final3.StoreUnsafe(ref dst, pop012);

            fieldIndex += (uint)BitOperations.PopCount(maskControl);
            goto ContinueRead;

            SlowPath:
            // clear the bits that are inside quotes
            maskControl &= ~Bithacks.FindQuoteMask(maskQuote, quotesConsumed);

            uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;

            ParseAnyArm64(
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
            goto ContinueRead;

            SumQuotes:
            quotesConsumed += (uint)BitOperations.PopCount(maskQuote);

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;

            hasCR = TCRLF.Value ? Vector256.Equals128(vector, vecCR) : default;
            hasLF = Vector256.Equals128(vector, vecLF);
            hasDelimiter = Vector256.Equals128(vector, vecDelim);
            hasQuote = Vector256.Equals128(vector, vecQuote);
            hasControl = hasLF | hasDelimiter;

            (maskControl, maskLF, maskQuote, maskCR) = AsciiVector.MoveMask<TCRLF>(hasControl, hasLF, hasQuote, hasCR);
        }

        return (int)fieldIndex;
    }
}

[InlineArray(8)]
file struct IArr8
{
    public byte elem0;
}


/*
| Method | Chars | Quoted | Newline | Mean     | StdDev  | Ratio |
|------- |------ |------- |-------- |---------:|--------:|------:|
| Simd   | False | False  | LF      | 613.9 us | 1.15 us |  1.00 |
| Arm    | False | False  | LF      | 539.2 us | 2.26 us |  0.88 |
|        |       |        |         |          |         |       |
| Simd   | False | False  | CRLF    | 758.9 us | 1.07 us |  1.00 |
| Arm    | False | False  | CRLF    | 557.5 us | 2.46 us |  0.73 |

| Method | Chars | Quoted | Newline | Mean     | StdDev  | Ratio |
|------- |------ |------- |-------- |---------:|--------:|------:|
| Simd   | False | False  | LF      | 618.1 us | 0.63 us |  1.00 |
| Arm    | False | False  | LF      | 540.0 us | 1.98 us |  0.87 |
|        |       |        |         |          |         |       |
| Simd   | False | False  | CRLF    | 757.4 us | 3.72 us |  1.00 |
| Arm    | False | False  | CRLF    | 557.6 us | 0.53 us |  0.74 |

| Method | Chars | Quoted | Newline | Mean     | StdDev  | Ratio |
|------- |------ |------- |-------- |---------:|--------:|------:|
| Simd   | False | False  | LF      | 620.9 us | 1.02 us |  1.00 |
| Arm    | False | False  | LF      | 540.0 us | 1.82 us |  0.87 |
|        |       |        |         |          |         |       |
| Simd   | False | False  | CRLF    | 763.1 us | 1.24 us |  1.00 |
| Arm    | False | False  | CRLF    | 553.7 us | 2.38 us |  0.73 |

| Method | Chars | Quoted | Newline | Mean     | StdDev  | Ratio |
|------- |------ |------- |-------- |---------:|--------:|------:|
| Simd   | False | False  | LF      | 616.5 us | 0.84 us |  1.00 |
| Arm    | False | False  | LF      | 528.7 us | 0.96 us |  0.86 |
|        |       |        |         |          |         |       |
| Simd   | False | False  | CRLF    | 762.4 us | 0.98 us |  1.00 |
| Arm    | False | False  | CRLF    | 565.3 us | 2.77 us |  0.74 |
*/
