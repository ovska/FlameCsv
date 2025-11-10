#if NET10_0_OR_GREATER
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx512Tokenizer
{
    public static bool IsSupported => Avx512Vbmi2.IsSupported;
}

[SkipLocalsInit]
internal sealed class Avx512Tokenizer<T, TNewline>(CsvOptions<T> options) : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    // leave space for 2 vectors;
    // vector count to avoid reading past the buffer
    // vector count for prefetching
    // and 1 for reading past the current vector to check two token sequences
    private static int EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector512<byte>.Count * 3;
    }

    private static int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector512<byte>.Count;
    }

    public override int PreferredLength => Vector512<byte>.Count * 4;
    public override int MinimumFieldBufferSize => MaxFieldsPerIteration;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer destination, int startIndex, ReadOnlySpan<T> data)
    {
        if (!Avx512Vbmi2.IsSupported)
        {
            // ensure the method is trimmed on NAOT
            throw new UnreachableException();
        }
        
        Debug.Assert(data.Length <= Field.MaxFieldEnd);

        if ((uint)(data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - EndOffset;
        nuint fieldEnd = (nuint)destination.Fields.Length - (nuint)MaxFieldsPerIteration;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination.Fields);
        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(destination.Quotes);

        nuint fieldIndex = 0;

        // load the constants into registers
        Vector512<byte> vecDelim = AsciiVector.Create512(_delimiter);
        Vector512<byte> vecQuote = AsciiVector.Create512(_quote);
        Vector512<byte> vecLF = AsciiVector.Create512((byte)'\n');

        Vector512<byte> vecCR;

        if (TNewline.IsCRLF)
        {
            vecCR = AsciiVector.Create512((byte)'\r');
        }
        else
        {
            Unsafe.SkipInit(out vecCR);
        }

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector512<byte> bit7 = Vector512.Create((byte)0x80);
        Vector512<int> msbAndBitsUpTo7 = Vector512.Create(fixupScalar);
        Vector512<uint> iterationLength = Vector512.Create((uint)Vector512<byte>.Count);

        uint quotesConsumed = 0;
        ulong crCarry = 0;

        Vector512<uint> indexVector;
        Vector512<byte> vector;

        unsafe
        {
            nint alignment = 64;
            nint remainder = ((nint)Unsafe.AsPointer(ref Unsafe.Add(ref first, index)) % alignment) / sizeof(T);

            if (remainder != 0)
            {
                nuint skip = (uint)alignment - (uint)remainder;

                Inline64<T> temp = default; // default zero-inits

                nuint idx = 0;
                ref T src = ref Unsafe.Add(ref first, (nuint)startIndex);
                ref T dst = ref Unsafe.Add(ref temp.elem0, (nuint)remainder);

                do
                {
                    Unsafe.Add(ref temp.elem0, idx) = Unsafe.Add(ref first, idx);
                    idx++;
                } while (idx < skip);

                vector = AsciiVector.Load512(ref temp.elem0, 0);

                // adjust separately; we need both uint32 and (n)uint64 to wrap correctly
                indexVector = Vector512.Create((uint)index - (uint)remainder);
                index -= (nuint)remainder;
            }
            else
            {
                vector = AsciiVector.LoadAligned512(ref first, index);
                indexVector = Vector512.Create((uint)index);
            }
        }

        Vector512<byte> nextVector = AsciiVector.LoadAligned512(ref first, index + (nuint)Vector512<byte>.Count);

        while (fieldIndex <= fieldEnd && index <= searchSpaceEnd)
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

            if (TNewline.IsCRLF)
            {
                maskCR = Vector512.Equals(vector, vecCR).ExtractMostSignificantBits();
                maskLF = hasLF.ExtractMostSignificantBits(); // queue the movemask
                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 63;
            }

            // prefetch 2 vectors ahead
            vector = nextVector;
            nextVector = AsciiVector.LoadAligned512(ref first, index + (nuint)(2 * Vector512<byte>.Count));

            if ((TNewline.IsCRLF ? (maskControl | shiftedCR) : maskControl) == 0)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                goto ContinueRead;
            }

            if (TNewline.IsCRLF && Bithacks.IsDisjointCR(maskLF, shiftedCR))
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

            Vector512<byte> taggedIndices = (hasLF & bit7) | Vector512<byte>.Indices; // compiles to vpternlogd
            Vector512<byte> packedAny = Avx512Vbmi2.Compress(Vector512<byte>.Zero, hasAny, taggedIndices);

            Vector512<int> fixup = TNewline.IsCRLF
                ? Vector512.Create(fixupScalar | (Unsafe.BitCast<bool, byte>(shiftedCR != 0)) << 30)
                : msbAndBitsUpTo7;

            Vector128<sbyte> lowestLane = Avx512F.ExtractVector128(packedAny, 0).AsSByte();

            // sign extend to preserve EOL bits
            Vector512<int> taggedIndexVector = Avx512F.ConvertToVector512Int32(lowestLane);

            Vector512<int> fixedTaggedVector = taggedIndexVector & fixup;

            Vector512<uint> result = fixedTaggedVector.AsUInt32() + indexVector;
            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            index += (nuint)Vector512<byte>.Count;
            indexVector += iterationLength;
            continue;

            SlowPath:
            if (!TNewline.IsCRLF)
            {
                maskLF = hasLF.ExtractMostSignificantBits();
            }

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

            goto ContinueRead;
        }

        return (int)fieldIndex;
    }
}
#endif
