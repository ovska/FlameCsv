#if NET10_0_OR_GREATER
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx512Tokenizer
{
    public static bool IsSupported => Avx512Vbmi2.IsSupported;
}

[SkipLocalsInit]
internal sealed class Avx512Tokenizer<T, TCRLF>(CsvOptions<T> options) : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
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

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override unsafe int TokenizeCore(FieldBuffer destination, int startIndex, T* start, T* end)
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

        nuint fieldEnd = (nuint)destination.Fields.Length - (nuint)MaxFieldsPerIteration;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination.Fields);
        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(destination.Quotes);

        nuint fieldIndex = 0;

        Vector512<byte> vecDelim = Vector512.Create(byte.CreateTruncating(_delimiter));
        Vector512<byte> vecQuote = Vector512.Create(byte.CreateTruncating(_quote));
        Vector512<byte> vecLF = Vector512.Create((byte)'\n');
        Vector512<byte> vecCR = TCRLF.Value ? Vector512.Create((byte)'\r') : default;

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector512<byte> bit7 = Vector512.Create((byte)0x80);
        Vector512<int> msbAndBitsUpTo7 = Vector512.Create(fixupScalar);
        Vector512<uint> iterationLength = Vector512.Create((uint)Vector512<byte>.Count);

        uint quotesConsumed = 0;
        ulong crCarry = 0;

        Vector512<uint> indexVector;
        Vector512<byte> vector;

        // align the start
        // TODO: benchmark whether unaligned loads are worth it on real-world buffer sizes and streaming;
        // currently only optimized for fully buffered data
        {
            nint alignment = 64;
            nint remainder = ((nint)pData % alignment) / sizeof(T);

            if (remainder != 0)
            {
                nuint skip = (uint)alignment - (uint)remainder;

                Inline64<T> temp = default; // default zero-inits

                nuint idx = 0;
                ref T src = ref Unsafe.AsRef<T>(start);
                ref T dst = ref Unsafe.Add(ref temp.elem0, (nuint)remainder);

                do
                {
                    Unsafe.Add(ref dst, idx) = Unsafe.Add(ref src, idx);
                    idx++;
                } while (idx < skip);

                // safe AsPointer; stack allocated struct
                vector = AsciiVector.Load512((T*)Unsafe.AsPointer(ref temp.elem0));

                // adjust separately; we need both uint32 and (n)uint64 to wrap correctly
                indexVector = Vector512.Create((uint)index - (uint)remainder);
                index -= (nuint)remainder;
                pData -= remainder;

                Debug.Assert(((nint)index + 64) >= 0);
            }
            else
            {
                vector = AsciiVector.LoadAligned512(pData);
                indexVector = Vector512.Create((uint)index);
            }
        }

        Vector512<byte> nextVector = AsciiVector.LoadAligned512(pData + (nuint)Vector512<byte>.Count);

        do
        {
            Vector512<byte> hasLF = Vector512.Equals(vector, vecLF);
            Vector512<byte> hasDelimiter = Vector512.Equals(vector, vecDelim);
            Vector512<byte> hasAny = hasLF | hasDelimiter;

            ulong maskControl = hasAny.ExtractMostSignificantBits();

            // getting the mask instantly saves a bit so hasQuote isn't bounced between registers
            ulong maskQuote = Vector512.Equals(vector, vecQuote).ExtractMostSignificantBits();

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

            if (quotesConsumed >= (uint)(byte.MaxValue - MaxFieldsPerIteration)) // constant folded
            {
                destination.DegenerateQuotes = true;
                break;
            }

            if ((TCRLF.Value ? (maskControl | shiftedCR) : maskControl) == 0)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                goto ContinueRead;
            }

            if (TCRLF.Value && Bithacks.IsDisjointCR(maskLF, shiftedCR))
            {
                // maskControl doesn't contain CR by default, add it so we can find lone CR's
                maskControl |= maskCR;
                goto PathologicalPath;
            }

            uint matchCount = (uint)BitOperations.PopCount(maskControl);

            // rare cases: quotes, or too many matches to fit in VPCOMPRESSB path
            if ((quotesConsumed | maskQuote) != 0 || matchCount > (uint)Vector512<int>.Count)
            {
                goto SlowPath;
            }

            // get an iota vector with the MSB set on newline positions
            Vector512<byte> taggedIndices = (hasLF & bit7) | Vector512<byte>.Indices; // compiles to vpternlogd

            // pack the indexes of all matches to the front
            Vector512<byte> packedAny = Avx512Vbmi2.Compress(
                merge: Vector512<byte>.Zero,
                mask: hasAny,
                value: taggedIndices
            );

            // create a fixup to keep only the low bits and the MSB (which is 1 on newlines)
            Vector512<int> fixup = TCRLF.Value
                ? Vector512.Create(fixupScalar | ((shiftedCR != 0).ToByte() << 30))
                : msbAndBitsUpTo7;

            // we already verified that matchCount is low enough; pick the lowest 16 bytes...
            Vector128<sbyte> lowestLane = Avx512F.ExtractVector128(packedAny, 0).AsSByte();

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
                firstQuote: ref firstQuote,
                fieldIndex: ref fieldIndex,
                quotesConsumed: ref quotesConsumed,
                maskControl: maskControl,
                maskLF: maskLF,
                maskQuote: ref maskQuote,
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
                    first: ref Unsafe.AsRef<T>(start),
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quoteRef: ref firstQuote,
                    quotesConsumed: ref quotesConsumed
                );

                ParsePathological(
                    maskControl: maskControl,
                    maskQuote: ref maskQuote,
                    first: ref Unsafe.AsRef<T>(start),
                    index: (uint)index,
                    fieldIndex: ref fieldIndex,
                    fieldRef: ref firstField,
                    quoteRef: ref firstQuote,
                    delimiter: _delimiter,
                    quotesConsumed: ref quotesConsumed
                );
            }

            goto ContinueRead;
        } while (fieldIndex <= fieldEnd && pData < end);

        return (int)fieldIndex;
    }
}

[InlineArray(64)]
file struct Inline64<T>
    where T : unmanaged
{
    public T elem0;
}
#endif
