using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class Avx2Tokenizer
{
    public static bool IsSupported => Avx2.IsSupported;
}

// a stackalloc incurs buffer overrun cookie penalty
[InlineArray(32)]
file struct Inline32<T>
    where T : unmanaged
{
    public T elem0;
}

[SkipLocalsInit]
internal sealed class Avx2Tokenizer<T, TNewline> : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    private static nuint EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nuint)Vector256<byte>.Count * 3;
    }

    private static int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector256<byte>.Count;
    }

    public override int PreferredLength => Vector256<byte>.Count * 4;

    public override int MinimumFieldBufferSize => MaxFieldsPerIteration;

    private readonly T _quote;
    private readonly T _delimiter;

    private const uint MaxIndex = int.MaxValue / 2;

    public Avx2Tokenizer(CsvOptions<T> options)
    {
        _quote = T.CreateTruncating(options.Quote);
        _delimiter = T.CreateTruncating(options.Delimiter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override unsafe int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data)
    {
        if ((uint)(data.Length - startIndex) < EndOffset || ((nint)(MaxIndex - EndOffset) <= startIndex))
        {
            return 0;
        }

        _ = CompressionTables.BlendMask; // ensure static ctor is run

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (nuint)startIndex;
        nuint searchSpaceEnd = (nuint)Math.Min(MaxIndex, data.Length) - EndOffset;
        nuint fieldEnd = (nuint)buffer.Fields.Length - (nuint)MaxFieldsPerIteration;
        nuint fieldIndex = 0;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
        scoped ref uint firstField = ref MemoryMarshal.GetReference(buffer.Fields);

        Vector256<byte> vecDelim = Vector256.Create(byte.CreateTruncating(_delimiter));
        Vector256<byte> vecQuote = Vector256.Create(byte.CreateTruncating(_quote));
        Vector256<byte> vecLF = Vector256.Create((byte)'\n');

        Unsafe.SkipInit(out Vector256<byte> vecCR);

        if (TNewline.IsCRLF)
        {
            vecCR = Vector256.Create((byte)'\r');
        }

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector256<byte> bit7 = Vector256.Create((byte)0x80);
        Vector256<int> msbAndBitsUpTo7 = Vector256.Create(fixupScalar);
        Vector256<uint> iterationLength = Vector256.Create((uint)Vector256<byte>.Count);
        Vector256<long> addConstant = Vector256.Create(0L, 0x0808080808080808, 0x1010101010101010, 0x1818181818181818);

        uint quotesConsumed = 0;
        uint crCarry = 0;

        Vector256<byte> vector;
        Vector256<uint> indexVector;

        {
            // ensure data is aligned to 32 bytes
            // for simplicity, always use element count
            nint remainder =
                ((nint)Unsafe.AsPointer(ref Unsafe.Add(ref first, index)) % Vector256<byte>.Count) / sizeof(T);

            if (remainder != 0)
            {
                int skip = Vector256<byte>.Count - (int)remainder;

                Inline32<T> temp = default; // default zero-inits

                // Copy the elements to the correct position in the buffer
                Unsafe.CopyBlockUnaligned(
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref temp.elem0, (nuint)remainder)),
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref first, (nuint)startIndex)),
                    byteCount: (uint)(skip * sizeof(T))
                );
                vector = AsciiVector.Load(ref temp[0], 0);

                // adjust separately; we need both uint32 and (n)uint64 to wrap correctly
                indexVector = Vector256.Create((uint)index - (uint)remainder);
                index -= (nuint)remainder;
            }
            else
            {
                vector = AsciiVector.LoadAligned256(ref first, index);
                indexVector = Vector256.Create((uint)index);
            }
        }

        Vector256<byte> nextVector = AsciiVector.Load(ref first, index + (nuint)Vector256<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.LoadAligned256(
                ref first,
                index + (nuint)(2 * Vector256<byte>.Count)
            );

            Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
            Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
            Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);
            Vector256<byte> hasAny = hasLF | hasDelimiter;

            uint maskControl = hasAny.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint maskLF);
            Unsafe.SkipInit(out uint shiftedCR);

            if (TNewline.IsCRLF)
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
                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                    goto ContinueRead;
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
                    // TODO: flip quote carry
                    quotesConsumed += (uint)BitOperations.PopCount(maskQuote);
                    goto ContinueRead;
                }
            }

            uint matchCount = (uint)BitOperations.PopCount(maskControl);

            // rare cases: quotes, or too many matches to fit in the compress path
            if ((quotesConsumed | maskQuote) != 0 || matchCount > (uint)Vector256<int>.Count)
            {
                goto SlowPath;
            }

            // build a mask to remove extra bits caused by sign-extension
            Vector256<int> fixup = TNewline.IsCRLF
                ? Vector256.Create(fixupScalar | ((shiftedCR != 0).ToByte() << 30))
                : msbAndBitsUpTo7;

            uint mask = ~maskControl;

            ref ulong table = ref Unsafe.AsRef(in CompressionTables.ThinEpi8[0]);

            byte mask1 = (byte)mask;
            byte mask2 = (byte)(mask >> 8);
            byte mask3 = (byte)(mask >> 16);
            byte mask4 = (byte)(mask >> 24);

            Vector256<ulong> shufmask = Vector256.Create(
                Unsafe.Add(ref table, mask1),
                Unsafe.Add(ref table, mask2),
                Unsafe.Add(ref table, mask3),
                Unsafe.Add(ref table, mask4)
            );

            Vector256<byte> tags = (hasLF & bit7);

            ref byte popCounts = ref Unsafe.AsRef(in CompressionTables.PopCountMult2[0]);

            // Add the constant to the shuffle mask.
            Vector256<byte> shufmaskBytes = shufmask.AsByte() + addConstant.AsByte();

            // Calculate the offset for the upper half.
            uint lowerCountOffset = (uint)BitOperations.PopCount(maskControl & 0xFFFF);

            byte pop1 = Unsafe.Add(ref popCounts, mask1);
            byte pop3 = Unsafe.Add(ref popCounts, mask3);

            Vector256<byte> taggedIndices = tags | Vector256<byte>.Indices;

            ref byte shuffleCombine = ref Unsafe.Add(ref Unsafe.AsRef(in CompressionTables.ShuffleCombine[0]), 16);

            // Load the 128-bit masks from pshufb_combine_table.
            Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, (nuint)pop1 * 8);
            Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, ((nuint)pop3 * 8) - lowerCountOffset);

            // Shuffle the source data.
            Vector256<byte> pruned = Avx2.Shuffle(taggedIndices, shufmaskBytes);

            // Combine the two 128-bit lanes into a 256-bit mask.
            Vector256<byte> compactmask = Vector256.Create(combine0, combine1);

            // Shuffle the pruned vector with the combined mask.
            Vector256<byte> almostthere = Avx2.Shuffle(pruned, compactmask);

            Vector128<byte> blend = Vector128.LoadAligned(CompressionTables.BlendMask + (lowerCountOffset * 16));

            Vector128<byte> upper = almostthere.GetUpper();
            Vector128<byte> lower = almostthere.GetLower();

            Vector128<byte> combined = Sse41.BlendVariable(lower, upper, blend);

            // use a sign-extended conversion to int32 to keep the CR/LF tags
            Vector256<int> taggedIndexVector = Avx2.ConvertToVector256Int32(combined.AsSByte());

            Vector256<int> fixedTaggedVector = taggedIndexVector & fixup;

            Vector256<uint> result = fixedTaggedVector.AsUInt32() + indexVector;
            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;
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
        } while (fieldIndex <= fieldEnd && index <= searchSpaceEnd);

        return (int)fieldIndex;
    }
}
