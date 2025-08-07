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

[SkipLocalsInit]
internal sealed class Avx2Tokenizer<T, TNewline>(CsvOptions<T> options) : CsvPartialTokenizer<T>
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
        get => (Vector256<byte>.Count * 3) + (TNewline.IsCRLF ? 1 : 0);
    }

    public override int PreferredLength => Vector256<byte>.Count * 4;

    private readonly T _quote = T.CreateTruncating(options.Quote);
    private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

    private const nuint MaxIndex = int.MaxValue / 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override bool Tokenize(RecordBuffer recordBuffer, ReadOnlySpan<T> data)
    {
        FieldBuffer destination = recordBuffer.GetUnreadBuffer(
            minimumLength: Vector256<byte>.Count,
            out int startIndex
        );

        if ((data.Length - startIndex) < EndOffset || (((nint)MaxIndex - EndOffset) <= startIndex))
        {
            return false;
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
        Vector256<byte> addConst = Vector256
            .Create(0, 0, 0x08080808, 0x08080808, 0x10101010, 0x10101010, 0x18181818, 0x18181818)
            .AsByte();

        uint quotesConsumed = 0;
        uint quoteCarry = 0;
        uint crCarry = 0;

        unsafe
        {
            // ensure data is aligned to 32 bytes
            nuint remainder =
                (nuint)Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex)) % (nuint)Vector256<byte>.Count;

            if (remainder != 0)
            {
                nuint bytesToSkip = (nuint)Vector256<byte>.Count - remainder;
                nuint elementsToSkip = bytesToSkip / (nuint)sizeof(T);
                runningIndex += elementsToSkip;
            }
        }

        Vector256<uint> runningIndexVector = Vector256.Create((uint)runningIndex);
        Vector256<byte> vector = AsciiVector.Load(ref first, runningIndex);
        Vector256<byte> nextVector = AsciiVector.Load(ref first, runningIndex + (nuint)Vector256<byte>.Count);

        while (fieldIndex <= fieldEnd && runningIndex <= searchSpaceEnd)
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
            if (quotesConsumed != 0 || maskQuote != 0 || matchCount > (uint)Vector256<int>.Count)
            {
                goto SlowPath;
            }

            Vector256<byte> taggedIndices = (hasLF & bit7) | Vector256<byte>.Indices;
            Vector256<int> taggedIndexVector;

            {
                uint mask = ~maskControl;

                // Split the mask into four bytes
                byte mask1 = (byte)mask;
                byte mask2 = (byte)(mask >> 8);
                byte mask3 = (byte)(mask >> 16);
                byte mask4 = (byte)(mask >> 24);

                // Build the 256-bit shuffle mask.
                ref ulong table = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);

                Vector256<byte> shufmask = Vector256
                    .Create(
                        Unsafe.Add(ref table, mask1),
                        Unsafe.Add(ref table, mask2),
                        Unsafe.Add(ref table, mask3),
                        Unsafe.Add(ref table, mask4)
                    )
                    .AsByte();

                // Add the constant to the shuffle mask.
                Vector256<byte> shufmaskBytes = shufmask + addConst;

                // Shuffle the source data.
                Vector256<byte> pruned = Avx2.Shuffle(taggedIndices, shufmaskBytes);

                // Use precomputed popcounts from the table.
                ref byte popCounts = ref Unsafe.AsRef(in CompressionTables.PopCountMult2[0]);
                byte pop1 = Unsafe.Add(ref popCounts, mask1);
                byte pop3 = Unsafe.Add(ref popCounts, mask3);

                // Load the 128-bit masks from pshufb_combine_table.
                ref readonly byte shuffleCombine = ref CompressionTables.ShuffleCombine[0];
                Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop1 * 8);
                Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop3 * 8);

                // Combine the two 128-bit lanes into a 256-bit mask.
                Vector256<byte> compactmask = Vector256.Create(combine0, combine1);

                // Shuffle the pruned vector with the combined mask.
                Vector256<byte> almostthere = Avx2.Shuffle(pruned, compactmask);

                // Extract the lower and upper 128-bit lanes.
                Vector128<byte> lower = almostthere.GetLower();
                Vector128<byte> upper = almostthere.GetUpper();

                // Calculate the offset for the upper half.
                uint lowerCountOffset = (uint)BitOperations.PopCount(maskControl & 0xFFFF) * 16;

                ref byte zeroUpperTable = ref Unsafe.AsRef(in CompressionTables.ZeroUpper[0]);
                ref byte compactShuffle = ref Unsafe.AsRef(in CompressionTables.CompactShuffle[0]);

                Vector128<byte> zeroUpper = Vector128.LoadUnsafe(in zeroUpperTable, lowerCountOffset);
                Vector128<byte> indices = Vector128.LoadUnsafe(in compactShuffle, lowerCountOffset);

                Vector128<byte> lowerZeroed = lower & zeroUpper;
                Vector128<byte> upperShuffled = Ssse3.Shuffle(upper, indices);

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
                Sse.Prefetch0(Unsafe.AsPointer(ref Unsafe.Add(ref first, runningIndex + (nuint)(512 / sizeof(T)))));
            }

            continue;

            SlowPath:
            uint quoteXOR = Bithacks.FindQuoteMask(maskQuote, ref quoteCarry);
            maskControl &= ~quoteXOR; // clear the bits that are inside quotes

            // JIT eliminates on LF parsers
            FieldFlag flag = (TNewline.IsCRLF && maskCR != 0) ? FieldFlag.CRLF : FieldFlag.EOL;

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

                FieldFlag isLF = Unsafe.SizeOf<T>() switch
                {
                    sizeof(byte) => value is (byte)'\n',
                    sizeof(char) => value is '\n',
                    _ => throw Token<T>.NotSupported,
                }
                    ? flag
                    : 0u;

                Unsafe.Add(ref firstField, fieldIndex) = offset | (uint)isLF;
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
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Compress(Vector256<byte> value, uint mask)
    {
        mask = ~mask;

        // Split the mask into four bytes
        byte mask1 = (byte)mask;
        byte mask2 = (byte)(mask >> 8);
        byte mask3 = (byte)(mask >> 16);
        byte mask4 = (byte)(mask >> 24);

        // Build the 256-bit shuffle mask.
        ref ulong table = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);

        Vector256<byte> shufmask = Vector256
            .Create(
                Unsafe.Add(ref table, mask1),
                Unsafe.Add(ref table, mask2),
                Unsafe.Add(ref table, mask3),
                Unsafe.Add(ref table, mask4)
            )
            .AsByte();

        // Create a constant to add to the shuffle mask.
        // When interpreted as 32 bytes in little-endian order, this constant is:
        // [ 0,0,0,0,... 8,8,8,8,... 16,16,16,16,...  24,24,24,24,... ]
        // We build it here using 8 ints (element 0 is the lowest).
        Vector256<byte> addConst = Vector256
            .Create(0, 0, 0x08080808, 0x08080808, 0x10101010, 0x10101010, 0x18181818, 0x18181818)
            .AsByte();

        // Add the constant to the shuffle mask.
        Vector256<byte> shufmaskBytes = shufmask + addConst;

        // Shuffle the source data.
        Vector256<byte> pruned = Avx2.Shuffle(value, shufmaskBytes);

        // Use precomputed popcounts from the table.
        ref byte popCounts = ref Unsafe.AsRef(in CompressionTables.PopCountMult2[0]);
        byte pop1 = Unsafe.Add(ref popCounts, mask1);
        byte pop3 = Unsafe.Add(ref popCounts, mask3);

        // Load the 128-bit masks from pshufb_combine_table.
        ref readonly byte shuffleCombine = ref CompressionTables.ShuffleCombine[0];
        Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop1 * 8);
        Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop3 * 8);

        // Combine the two 128-bit lanes into a 256-bit mask.
        Vector256<byte> compactmask = Vector256.Create(combine0, combine1);

        // Shuffle the pruned vector with the combined mask.
        Vector256<byte> almostthere = Avx2.Shuffle(pruned, compactmask);

        // Extract the lower and upper 128-bit lanes.
        Vector128<byte> lower = almostthere.GetLower();
        Vector128<byte> upper = almostthere.GetUpper();

        // Calculate the offset for the upper half.
        int lowerCount = BitOperations.PopCount(~mask & 0xFFFF);

        Vector128<byte> zeroUpper = Vector128.LoadUnsafe(in CompressionTables.ZeroUpper[lowerCount * 16]);
        Vector128<byte> indices = Vector128.LoadUnsafe(in CompressionTables.CompactShuffle[(byte)(lowerCount * 16)]);

        Vector128<byte> lowerZeroed = lower & zeroUpper;
        Vector128<byte> upperShuffled = Avx2.Shuffle(upper, indices);

        Vector128<byte> result = lowerZeroed | upperShuffled;

        return Avx2.ConvertToVector256Int32(result.AsSByte());
    }
}
