using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx2Tokenizer
{
    public static bool IsSupported => Bmi1.IsSupported && Avx2.IsSupported;
}

// a stackalloc incurs buffer overrun cookie penalty
[InlineArray(32)]
file struct Inline32<T>
    where T : unmanaged
{
    public T elem0;
}

[SkipLocalsInit]
internal sealed class Avx2Tokenizer<T, TNewline>(CsvOptions<T> options) : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    // leave space for 2 vectors;
    // vector count to avoid reading past the buffer
    // vector count for prefetching
    // and 1 for reading past the current vector to check two token sequences
    private static nuint EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nuint)Vector256<byte>.Count * 3;
    }

    public override int PreferredLength => Vector256<byte>.Count * 4;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    private const uint MaxIndex = int.MaxValue / 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override bool Tokenize(RecordBuffer recordBuffer, ReadOnlySpan<T> data)
    {
        FieldBuffer destination = recordBuffer.GetUnreadBuffer(
            minimumLength: Vector256<byte>.Count,
            out int startIndex
        );

        if ((uint)(data.Length - startIndex) < EndOffset || ((nint)(MaxIndex - EndOffset) <= startIndex))
        {
            return false;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (nuint)startIndex;
        nuint searchSpaceEnd = (nuint)Math.Min(MaxIndex, data.Length) - EndOffset;
        nuint fieldEnd = (nuint)destination.Fields.Length - EndOffset;
        nuint fieldIndex = 0;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        scoped ref byte firstFlags = ref MemoryMarshal.GetReference(destination.Quotes);
        scoped ref uint firstField = ref MemoryMarshal.GetReference(destination.Fields);

        // load the constants into registers
        T delimiter = _delimiter;
        Vector256<byte> vecDelim = AsciiVector.Create(_delimiter);
        Vector256<byte> vecQuote = AsciiVector.Create(_quote);
        Vector256<byte> vecLF = AsciiVector.Create((byte)'\n');

        Unsafe.SkipInit(out Vector256<byte> vecCR);

        if (TNewline.IsCRLF)
        {
            vecCR = AsciiVector.Create((byte)'\r');
        }

        Vector256<byte> bit7 = Vector256.Create((byte)0x80);
        Vector256<int> msbAndBitsUpTo7 = Vector256.Create(unchecked((int)0x8000007F));
        Vector256<uint> iterationLength = Vector256.Create((uint)Vector256<byte>.Count);
        Vector256<byte> add = Vector256.Create(0L, 0x0808080808080808, 0x1010101010101010, 0x1818181818181818).AsByte();

        uint quotesConsumed = 0;
        uint quoteCarry = 0;
        uint crCarry = 0;

        Vector256<byte> vector;
        Vector256<uint> runningIndexVector;

        unsafe
        {
            // ensure data is aligned to 32 bytes
            // for simplicity, always use element count
            nint remainder =
                ((nint)Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex)) % Vector256<byte>.Count) / sizeof(T);

            if (remainder != 0)
            {
                int skip = Vector256<byte>.Count - (int)remainder;

                Inline32<T> buffer = default; // default zero-inits

                // Copy the elements to the correct position in the buffer
                Unsafe.CopyBlockUnaligned(
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref buffer.elem0, (nuint)remainder)),
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref first, (nuint)startIndex)),
                    byteCount: (uint)(skip * sizeof(T))
                );
                vector = AsciiVector.Load(ref buffer[0], 0);

                // adjust separately; we need both uint32 and (n)uint64 to wrap correctly
                runningIndexVector = Vector256.Create((uint)runningIndex - (uint)remainder);
                runningIndex -= (nuint)remainder;
            }
            else
            {
                vector = AsciiVector.Load(ref first, runningIndex);
                runningIndexVector = Vector256.Create((uint)runningIndex);
            }
        }

        Vector256<byte> nextVector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

        do
        {
            Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
            Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
            Vector256<byte> hasAny = hasLF | hasDelimiter;

            // getting the mask instantly saves a bit so quote isn't bounced between registers
            uint maskQuote = Vector256.Equals(vector, vecQuote).ExtractMostSignificantBits();
            uint maskControl = hasAny.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint maskCR);
            Unsafe.SkipInit(out uint maskLF);
            Unsafe.SkipInit(out uint shiftedCR);

            if (TNewline.IsCRLF)
            {
                maskCR = Vector256.Equals(vector, vecCR).ExtractMostSignificantBits();
                maskLF = hasLF.ExtractMostSignificantBits(); // queue the movemask
                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 31;
            }

            // prefetch 2 vectors ahead
            vector = nextVector;
            nextVector = AsciiVector.LoadAligned256(ref first, runningIndex + (nuint)(2 * Vector256<byte>.Count));

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

            // rare cases: quotes, or too many matches to fit in the compress path
            if ((quotesConsumed | maskQuote) != 0 || matchCount > (uint)Vector256<int>.Count)
            {
                goto SlowPath;
            }

            Vector256<int> taggedIndexVector;

            unsafe
            {
                uint mask = ~maskControl;

                // Split the mask into four bytes
                byte mask1 = (byte)mask;
                byte mask2 = (byte)(mask >> 8);
                byte mask3 = (byte)(mask >> 16);
                byte mask4 = (byte)(mask >> 24);

                // Build the 256-bit shuffle mask.
                ref ulong table = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);

                Vector256<ulong> shufmask = Vector256.Create(
                    Unsafe.Add(ref table, mask1),
                    Unsafe.Add(ref table, mask2),
                    Unsafe.Add(ref table, mask3),
                    Unsafe.Add(ref table, mask4)
                );

                Vector256<byte> tags = (hasLF & bit7);

                // Add the constant to the shuffle mask.
                Vector256<byte> shufmaskBytes = shufmask.AsByte() + add;

                // Use precomputed popcounts from the table.
                ref byte popCounts = ref Unsafe.AsRef(in CompressionTables.PopCountMult2[0]);
                byte pop1 = Unsafe.Add(ref popCounts, mask1);
                byte pop3 = Unsafe.Add(ref popCounts, mask3);

                Vector256<byte> taggedIndices = tags | Vector256<byte>.Indices;

                // Load the 128-bit masks from pshufb_combine_table.
                ref readonly byte shuffleCombine = ref CompressionTables.ShuffleCombine[0];
                Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop1 * 8);
                Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop3 * 8);

                // Shuffle the source data.
                Vector256<byte> pruned = Avx2.Shuffle(taggedIndices, shufmaskBytes);

                // Combine the two 128-bit lanes into a 256-bit mask.
                Vector256<byte> compactmask = Vector256.Create(combine0, combine1);

                // Calculate the offset for the upper half.
                uint lowerCountOffset = (uint)BitOperations.PopCount(maskControl & 0xFFFF) * 16;

                // Shuffle the pruned vector with the combined mask.
                Vector256<byte> almostthere = Avx2.Shuffle(pruned, compactmask);

                Vector128<byte> indices = Vector128.LoadAligned(CompressionTables.CompactShuffle + lowerCountOffset);
                Vector128<byte> zeroUpper = Vector128.LoadAligned(CompressionTables.ZeroUpper + lowerCountOffset);

                // Extract the lower and upper 128-bit lanes.
                Vector128<byte> upper = almostthere.GetUpper();
                Vector128<byte> lower = almostthere.GetLower();

                Vector128<byte> upperShuffled = Ssse3.Shuffle(upper, indices);
                Vector128<byte> lowerZeroed = lower & zeroUpper;

                Vector128<byte> combined = lowerZeroed | upperShuffled;

                taggedIndexVector = Avx2.ConvertToVector256Int32(combined.AsSByte());
            }

            Vector256<int> fixup = TNewline.IsCRLF
                ? (msbAndBitsUpTo7 | Vector256.Create((Unsafe.BitCast<bool, byte>(shiftedCR != 0)) << 30))
                : msbAndBitsUpTo7;

            Vector256<int> fixedTaggedVector = taggedIndexVector & fixup;

            Vector256<uint> result = fixedTaggedVector.AsUInt32() + runningIndexVector;
            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            runningIndex += (nuint)Vector256<byte>.Count;
            runningIndexVector += iterationLength;

            unsafe
            {
                nuint prefetch = runningIndex + (nuint)(512u / sizeof(T));
                Sse.Prefetch0(Unsafe.AsPointer(ref Unsafe.Add(ref first, prefetch)));
            }

            continue;

            SlowPath:
            uint quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            Unsafe.SkipInit(out byte isCR);
            uint eolFlag = Field.IsEOL;

            if (TNewline.IsCRLF)
            {
                isCR = Unsafe.BitCast<bool, byte>(shiftedCR != 0);
                eolFlag |= (uint)(isCR << 30);
            }

            // quoteXOR might have zeroed out the mask so do..while won't work
            while (maskControl != 0)
            {
                uint pos = (uint)BitOperations.TrailingZeroCount(maskControl);
                uint maskUpToPos = Bmi1.GetMaskUpToLowestSetBit(maskControl);

                uint offset = (uint)runningIndex + pos;
                quotesConsumed += (uint)BitOperations.PopCount(maskQuote & maskUpToPos);

                T value = Unsafe.Add(ref first, offset); // queue the load

                // consume masks
                maskControl &= ~maskUpToPos;
                maskQuote &= ~maskUpToPos;

                Field.SaturateTo7Bits(ref quotesConsumed);

                bool isLF = Unsafe.SizeOf<T>() switch
                {
                    sizeof(byte) => Unsafe.BitCast<T, byte>(value) is (byte)'\n',
                    sizeof(char) => Unsafe.BitCast<T, char>(value) is '\n',
                    _ => throw Token<T>.NotSupported,
                };

                if (TNewline.IsCRLF)
                {
                    offset -= (uint)(Unsafe.BitCast<bool, byte>(isLF) & isCR);
                }

                uint isFieldEOL = isLF ? eolFlag : 0;

                Unsafe.Add(ref firstField, fieldIndex) = offset | isFieldEOL;
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
                    uint maskUpToPos = Bmi1.GetMaskUpToLowestSetBit(maskControl);

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
        } while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd);

        recordBuffer.SetFieldsRead((int)fieldIndex);
        return fieldIndex > 0;
    }
}
