#if NET10_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx512Tokenizer
{
    public static bool IsSupported => Avx512Vbmi2.IsSupported;
}

[SkipLocalsInit]
internal sealed class Avx512Tokenizer<T, TCRLF, TQuote> : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
    where TQuote : struct, IConstant
{
    // leave space for 2 vectors;
    // vector count to avoid reading past the buffer
    // vector count for prefetching
    // and 1 for reading past the current vector to check two token sequences
    protected override int Overscan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector512<byte>.Count * 3;
    }

    public override int PreferredLength => Vector512<byte>.Count * 4;
    public override int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector512<byte>.Count;
    }

    private readonly byte _quote;
    private readonly byte _delimiter;

    public Avx512Tokenizer(CsvOptions<T> options)
    {
        Check.Equal(TCRLF.Value, options.Newline.IsCRLF(), "CRLF constant must match newline option.");
        Check.Equal(TQuote.Value, options.Quote.HasValue, "Quote constant must match presence of quote char.");
        _quote = (byte)options.Quote.GetValueOrDefault();
        _delimiter = (byte)options.Delimiter;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end)
    {
        if (!Avx512Vbmi2.IsSupported)
        {
            // ensure the method is trimmed on NAOT
            throw new PlatformNotSupportedException();
        }

#if false
        ReadOnlySpan<T> data = new(start, (int)(end - start));
#endif

        nuint index = (uint)startIndex;
        T* pData = start + index;

        nuint fieldEnd = (nuint)destination.Length - (nuint)MaxFieldsPerIteration;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination);

        nuint fieldIndex = 0;

        Vector512<byte> vecDelim = Vector512.Create(_delimiter);
        Vector512<byte> vecQuote = TQuote.Value ? Vector512.Create(_quote) : default;
        Vector512<byte> vecLF = Vector512.Create((byte)'\n');
        Vector512<byte> vecCR = TCRLF.Value ? Vector512.Create((byte)'\r') : default;

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector512<byte> bit7 = Vector512.Create((byte)0x80);
        Vector512<int> msbAndBitsUpTo7 = Vector512.Create(fixupScalar);
        Vector512<uint> iterationLength = Vector512.Create((uint)Vector512<byte>.Count);

        uint quotesConsumed = 0;
        ulong crCarry = 0;

        Vector512<byte> vector = AsciiVector.Load512(pData);
        Vector512<uint> indexVector;

        nint remainder = ((nint)pData % Vector512<byte>.Count) / sizeof(T);

        if (remainder != 0)
        {
            vector = AsciiVector.ShiftItemsRight(vector, (int)remainder);
            indexVector = Vector512.Create((uint)index - (uint)remainder);
            index -= (nuint)remainder;
            pData -= remainder;
        }
        else
        {
            indexVector = Vector512.Create((uint)index);
        }

        Vector512<byte> nextVector = AsciiVector.LoadAligned512(pData + (nuint)Vector512<byte>.Count);

        do
        {
            Vector512<byte> hasLF = Vector512.Equals(vector, vecLF);
            Vector512<byte> hasDelimiter = Vector512.Equals(vector, vecDelim);
            Vector512<byte> hasControl = hasLF | hasDelimiter;

            ulong maskControl = hasControl.ExtractMostSignificantBits();

            // getting the mask instantly saves a bit so hasQuote isn't bounced between registers
            ulong maskQuote = TQuote.Value ? Vector512.Equals(vector, vecQuote).ExtractMostSignificantBits() : default;

            Unsafe.SkipInit(out ulong maskCR);
            Unsafe.SkipInit(out ulong maskLF);
            Unsafe.SkipInit(out ulong shiftedCR);

            if (TCRLF.Value)
            {
                maskCR = Vector512.Equals(vector, vecCR).ExtractMostSignificantBits();
                maskLF = hasLF.ExtractMostSignificantBits(); // queue the movemask
                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 63;
            }

            // prefetch 2 vectors ahead
            vector = nextVector;
            nextVector = AsciiVector.LoadAligned512(pData + (nuint)(2 * Vector512<byte>.Count));

            if ((TCRLF.Value ? (maskControl | shiftedCR) : maskControl) == 0)
            {
                if (TQuote.Value)
                {
                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                }
                goto ContinueRead;
            }

            if (TCRLF.Value && Bithacks.IsDisjointCR(maskLF, shiftedCR))
            {
                return -1; // broken data
            }

            uint matchCount = (uint)BitOperations.PopCount(maskControl);

            if (TQuote.Value && (quotesConsumed | maskQuote) != 0)
            {
                goto SlowPath;
            }

            // too many matches to fit in VPCOMPRESSB path?
            if (matchCount > (uint)Vector512<int>.Count)
            {
                if (!TCRLF.Value)
                {
                    // maskLF is loaded lazily with CRLF disabled
                    maskLF = hasLF.ExtractMostSignificantBits();
                }

                uint flag = TCRLF.Value ? Bithacks.GetSubractionFlag(shiftedCR == 0) : Field.IsEOL;
                ParseControls((uint)index, ref Unsafe.Add(ref firstField, fieldIndex), maskControl, maskLF, flag);
                fieldIndex += matchCount;
                goto ContinueRead;
            }

            // get an iota vector with the MSB set on newline positions
            Vector512<byte> taggedIndices = (hasLF & bit7) | Vector512<byte>.Indices; // compiles to vpternlogd

            // pack the indexes of all matches to the front
            Vector512<byte> packedControlIndices = Avx512Vbmi2.Compress(
                merge: Vector512<byte>.Zero,
                mask: hasControl,
                value: taggedIndices
            );

            // create a fixup to keep only the low bits and the MSB (which is 1 on newlines)
            Vector512<int> fixup = TCRLF.Value
                ? Vector512.Create(fixupScalar | ((shiftedCR != 0).ToByte() << 30))
                : msbAndBitsUpTo7;

            // we already verified that matchCount is low enough; pick the lowest 16 bytes...
            Vector128<sbyte> lowestLane = Avx512F.ExtractVector128(packedControlIndices, 0).AsSByte();

            // ...and sign extend to int32 to preserve bits above 7 if the match was an EOL
            Vector512<int> taggedIndexVector = Avx512F.ConvertToVector512Int32(lowestLane);

            // preserve only MSB (and the next most significant bit, if the matches were CRLF pairs)
            Vector512<int> fixedTaggedVector = taggedIndexVector & fixup;

            // add the base index to the iota to get the final field indexes
            Vector512<uint> result = fixedTaggedVector.AsUInt32() + indexVector;
            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            index += (nuint)Vector512<byte>.Count;
            pData += Vector512<byte>.Count;
            indexVector += iterationLength;
            continue;

            SlowPath:
            Check.True(TQuote.Value, "SlowPath should only be taken when quotes are enabled.");
            if (TQuote.Value) // trim this codepath
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

                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
            }

            goto ContinueRead;
        } while (fieldIndex <= fieldEnd && pData < end);

        return (int)fieldIndex;
    }
}
#endif

[InlineArray(64)]
internal struct Inline64<T>
    where T : unmanaged
{
    public T elem0;
}

[InlineArray(128)]
internal struct Inline128<T>
    where T : unmanaged
{
    public T elem0;
}
