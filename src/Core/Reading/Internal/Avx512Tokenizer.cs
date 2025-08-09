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
    public static bool IsSupported => Bmi1.X64.IsSupported && Avx512Vbmi2.IsSupported;
}

// a stackalloc incurs buffer overrun cookie penalty
[InlineArray(64)]
file struct Inline64<T>
    where T : unmanaged
{
    public T elem0;
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
        get => (Vector512<byte>.Count * 3) + (TNewline.IsCRLF ? 1 : 0);
    }

    public override int PreferredLength => Vector512<byte>.Count * 4;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    private const nuint MaxIndex = int.MaxValue / 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(RecordBuffer recordBuffer, ReadOnlySpan<T> data)
    {
        FieldBuffer destination = recordBuffer.GetUnreadBuffer(
            minimumLength: Vector256<byte>.Count,
            out int startIndex
        );

        if ((data.Length - startIndex) < EndOffset || (((nint)MaxIndex - EndOffset) <= startIndex))
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (uint)startIndex;
        nuint searchSpaceEnd = Math.Min(MaxIndex, (nuint)data.Length) - (nuint)EndOffset;
        nuint fieldEnd = (nuint)destination.Fields.Length - (nuint)EndOffset;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        scoped ref byte firstFlags = ref MemoryMarshal.GetReference(destination.Quotes);
        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination.Fields);

        nuint fieldIndex = 0;

        // load the constants into registers
        T delimiter = _delimiter;
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
        ulong quoteCarry = 0;
        ulong crCarry = 0;

        Vector512<uint> runningIndexVector;
        Vector512<byte> vector;

        unsafe
        {
            // ensure data is aligned to 64 bytes
            // for simplicity, always use element count
            nint remainder =
                ((nint)Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex)) % Vector512<byte>.Count) / sizeof(T);

            if (remainder != 0)
            {
                int skip = Vector256<byte>.Count - (int)remainder;

                Inline64<T> buffer = default; // default zero-inits

                // Copy the elements to the correct position in the buffer
                Unsafe.CopyBlockUnaligned(
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref buffer.elem0, (nuint)remainder)),
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref first, (nuint)startIndex)),
                    byteCount: (uint)(skip * sizeof(T))
                );
                vector = AsciiVector.Load512(ref buffer[0], 0);

                // adjust separately; we need both uint32 and (n)uint64 to wrap correctly
                runningIndexVector = Vector512.Create((uint)runningIndex - (uint)remainder);
                runningIndex -= (nuint)remainder;
            }
            else
            {
                vector = AsciiVector.Load512(ref first, runningIndex);
                runningIndexVector = Vector512.Create((uint)runningIndex);
            }
        }

        Vector512<byte> nextVector = AsciiVector.LoadAligned512(ref first, runningIndex + (nuint)Vector512<byte>.Count);

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
        {
            Vector512<byte> hasLF = Vector512.Equals(vector, vecLF);
            Vector512<byte> hasDelimiter = Vector512.Equals(vector, vecDelim);
            Vector512<byte> hasAny = hasLF | hasDelimiter;

            // getting the mask instantly saves a bit so quote isn't bounced between registers
            ulong maskQuote = Vector512.Equals(vector, vecQuote).ExtractMostSignificantBits();
            ulong maskControl = hasAny.ExtractMostSignificantBits();

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
            nextVector = AsciiVector.LoadAligned512(ref first, runningIndex + (nuint)(2 * Vector512<byte>.Count));

            if ((TNewline.IsCRLF ? (maskControl | shiftedCR) : maskControl) == 0)
            {
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                goto ContinueRead;
            }

            if (TNewline.IsCRLF && (shiftedCR & (shiftedCR ^ maskLF)) != 0)
            {
                // maskControl doesn't contain CR by default, add it so we can find lone CR's
                maskControl |= maskCR;
                goto PathologicalPath;
            }

            uint matchCount = (uint)BitOperations.PopCount(maskControl);

            // rare cases: quotes, or too many matches to fit in VPCOMPRESSB path
            if (quotesConsumed != 0 || maskQuote != 0 || matchCount > (uint)Vector512<int>.Count)
            {
                goto SlowPath;
            }

            Vector512<byte> taggedIndices = Avx512F.TernaryLogic(Vector512<byte>.Indices, hasLF, bit7, 0xF8);
            Vector512<byte> packedAny = Avx512Vbmi2.Compress(Vector512<byte>.Zero, hasAny, taggedIndices);

            Vector512<int> fixup = TNewline.IsCRLF
                ? Vector512.Create(fixupScalar | (Unsafe.BitCast<bool, byte>(shiftedCR != 0)) << 30)
                : msbAndBitsUpTo7;

            Vector128<sbyte> lowestLane = Avx512F.ExtractVector128(packedAny, 0).AsSByte();
            Vector512<int> taggedIndexVector = Avx512F.ConvertToVector512Int32(lowestLane);

            Vector512<int> fixedTaggedVector = taggedIndexVector & fixup;

            Vector512<uint> result = fixedTaggedVector.AsUInt32() + runningIndexVector;
            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            runningIndex += (nuint)Vector512<byte>.Count;
            runningIndexVector += iterationLength;

            unsafe
            {
                Sse.Prefetch0(Unsafe.AsPointer(ref Unsafe.Add(ref first, (runningIndex + (nuint)(512 / sizeof(T))))));
            }

            continue;

            SlowPath:
            if (!TNewline.IsCRLF)
            {
                maskLF = hasLF.ExtractMostSignificantBits();
            }

            ulong quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            Unsafe.SkipInit(out byte isCR);
            uint eolType = Field.IsEOL;

            if (TNewline.IsCRLF)
            {
                isCR = Unsafe.BitCast<bool, byte>(shiftedCR != 0);
                eolType |= (uint)(isCR << 30);
            }

            // quoteXOR might have zeroed out the mask so do..while won't work
            while (maskControl != 0)
            {
                ulong maskUpToPos = Bmi1.X64.GetMaskUpToLowestSetBit(maskControl);
                uint pos = (uint)BitOperations.TrailingZeroCount(maskControl);

                quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);
                ulong posBit = (maskUpToPos ^ (maskUpToPos >> 1)); // extract the lowest set bit
                uint offset = (uint)runningIndex + pos;

                // consume masks
                maskControl &= ~maskUpToPos;
                maskQuote &= ~maskUpToPos;

                bool isLF = (maskLF & posBit) != 0;

                Field.SaturateTo7Bits(ref quotesConsumed);

                uint eolFlag = isLF ? eolType : 0;

                if (TNewline.IsCRLF)
                {
                    offset -= (uint)(Unsafe.BitCast<bool, byte>(isLF) & isCR);
                }

                Unsafe.Add(ref firstField, fieldIndex) = offset | eolFlag;
                Unsafe.Add(ref firstFlags, fieldIndex) = (byte)quotesConsumed;

                quotesConsumed = 0;
                fieldIndex++;
            }

            quotesConsumed += (uint)BitOperations.PopCount(maskQuote);

            goto ContinueRead;

            // TODO check cr carry
            PathologicalPath:
            // this branch here is just to eliminate the branch in LF parser where it's uneachable
            if (TNewline.IsCRLF)
            {
                quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
                maskControl &= ~quoteXOR; // clear the bits that are inside quotes

                // quoteXOR might have zeroed out the mask so do..while won't work
                while (maskControl != 0)
                {
                    uint pos = (uint)BitOperations.TrailingZeroCount(maskControl);
                    ulong maskUpToPos = Bmi1.X64.GetMaskUpToLowestSetBit(maskControl);

                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);

                    maskControl &= (maskControl - 1);
                    maskQuote &= ~maskUpToPos; // consume

                    Field.SaturateTo7Bits(ref quotesConsumed);

                    uint newlineFlag2 = (uint)
                        TNewline.IsNewline(delimiter, ref Unsafe.Add(ref first, pos + runningIndex), ref maskControl);

                    Unsafe.Add(ref firstField, fieldIndex) = (uint)(runningIndex + pos) | newlineFlag2;
                    Unsafe.Add(ref firstFlags, fieldIndex) = (byte)quotesConsumed;

                    quotesConsumed = 0;
                    fieldIndex++;
                }

                quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
            }

            goto ContinueRead;
        }

        return (int)fieldIndex;
    }
}
#endif
