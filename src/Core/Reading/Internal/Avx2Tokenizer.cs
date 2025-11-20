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
    public static bool IsSupported =>
        Avx2.IsSupported && RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64;
}

[SkipLocalsInit]
internal sealed class Avx2Tokenizer<T, TCRLF> : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
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

    public Avx2Tokenizer(CsvOptions<T> options)
    {
        _quote = T.CreateTruncating(options.Quote);
        _delimiter = T.CreateTruncating(options.Delimiter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data)
    {
        if (!Avx2.IsSupported)
        {
            // ensure the method is trimmed on NAOT
            throw new UnreachableException();
        }

        Debug.Assert(data.Length <= Field.MaxFieldEnd);

        if ((uint)(data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        unsafe
        {
            _ = CompressionTables.BlendMask; // ensure static ctor is run
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (nuint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - EndOffset;
        nuint fieldEnd = (nuint)buffer.Fields.Length - (nuint)MaxFieldsPerIteration;
        nuint fieldIndex = 0;

        Debug.Assert(searchSpaceEnd < (nuint)data.Length);

        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
        scoped ref uint firstField = ref MemoryMarshal.GetReference(buffer.Fields);

        Vector256<byte> vecDelim = Vector256.Create(byte.CreateTruncating(_delimiter));
        Vector256<byte> vecQuote = Vector256.Create(byte.CreateTruncating(_quote));
        Vector256<byte> vecLF = Vector256.Create((byte)'\n');
        Vector256<byte> msb = Vector256.Create((byte)0x80);
        Vector256<long> addCnst = Vector256.Create(0L, 0x0808080808080808, 0x1010101010101010, 0x1818181818181818);

        Unsafe.SkipInit(out Vector256<byte> vecCR);

        if (TCRLF.Value)
        {
            vecCR = Vector256.Create((byte)'\r');
        }

        const int fixupScalar = unchecked((int)0x8000007F);

        Vector256<uint> iterationLength = Vector256.Create((uint)Vector256<byte>.Count);

        uint quotesConsumed = 0;
        uint crCarry = 0;

        Vector256<byte> vector;
        Vector256<uint> indexVector;
        vector = AsciiVector.Load256(ref first, index);
        indexVector = Vector256.Create((uint)index);

        Vector256<byte> nextVector = AsciiVector.Load256(ref first, index + (nuint)Vector256<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector256<byte> prefetchVector = AsciiVector.Load256(ref first, index + (2 * (nuint)Vector256<byte>.Count));

            Vector256<byte> hasLF = Vector256.Equals(vector, vecLF);
            Vector256<byte> hasDelimiter = Vector256.Equals(vector, vecDelim);
            Vector256<byte> hasQuote = Vector256.Equals(vector, vecQuote);
            Vector256<byte> hasAny = hasLF | hasDelimiter;

            uint maskControl = hasAny.ExtractMostSignificantBits();
            uint maskQuote = hasQuote.ExtractMostSignificantBits();

            Unsafe.SkipInit(out uint maskLF); // calculated only on-demand for LF newlines
            Unsafe.SkipInit(out uint shiftedCR); // never used on LF newlines

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

            // tag the indices with the MSB if they are newlines
            Vector256<byte> taggedIndices = (hasLF & msb) | Vector256<byte>.Indices;

            // jit optimizes this add to a constant address
            ref byte shuffleCombine = ref Unsafe.Add(ref Unsafe.AsRef(in CompressionTables.ShuffleCombine[0]), 16);

            // Load the 128-bit masks from pshufb_combine_table
            // note that the upper lane is offset to the correct position already, e.g.
            // < 1 2 0 0 0 ... 3 4 5 ... > will leave 2 items empty on the upper lane
            Vector128<byte> combine0 = Vector128.LoadUnsafe(in shuffleCombine, (uint)pop1 * 8);
            Vector128<byte> combine1 = Vector128.LoadUnsafe(in shuffleCombine, ((uint)pop3 * 8) - lowerCountOffset);

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

            // add the base offset to the "tzcnts"
            Vector256<uint> result = fixedTaggedVector + indexVector;

            if (TCRLF.Value)
            {
                // subtract 1 from CRLF matches
                result -= ((fixedTaggedVector >> 30) & Vector256<uint>.One);
            }

            result.StoreUnsafe(ref firstField, fieldIndex);
            fieldIndex += matchCount;

            ContinueRead:
            index += (nuint)Vector256<byte>.Count;
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
        } while (fieldIndex <= fieldEnd && index <= searchSpaceEnd);

        return (int)fieldIndex;
    }
}
