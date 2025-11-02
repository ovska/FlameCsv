using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class ArmTokenizer
{
    public static bool IsSupported => AdvSimd.Arm64.IsSupported;
}

[SkipLocalsInit]
internal sealed class ArmTokenizer<T, TNewline> : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    private static nuint EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nuint)MaxFieldsPerIteration * 2;
    }

    private static int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128<byte>.Count * 4;
    }

    public override int PreferredLength => MaxFieldsPerIteration * 4;

    public override int MinimumFieldBufferSize => MaxFieldsPerIteration;

    private readonly T _quote;
    private readonly T _delimiter;

    public ArmTokenizer(CsvOptions<T> options)
    {
        _quote = T.CreateTruncating(options.Quote);
        _delimiter = T.CreateTruncating(options.Delimiter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data)
    {
        Debug.Assert(data.Length <= Field.MaxFieldEnd);

        if ((uint)(data.Length - startIndex) < EndOffset)
        {
            return 0;
        }

        scoped ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (uint)startIndex;
        nuint searchSpaceEnd = (nuint)data.Length - EndOffset;

        scoped ref uint firstField = ref MemoryMarshal.GetReference(buffer.Fields);
        scoped ref byte firstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
        nuint fieldIndex = 0;
        nuint fieldEnd = Math.Max(0, (nuint)buffer.Fields.Length - (nuint)MaxFieldsPerIteration);

        // ensure the worst case doesn't read past the end (e.g. data ends in Vector.Count delimiters)
        // we do this so there are no bounds checks in the loops
        Debug.Assert(searchSpaceEnd < (nuint)data.Length);
        Debug.Assert(buffer.Fields.Length >= Vector512<byte>.Count);
        Debug.Assert(buffer.Quotes.Length >= Vector512<byte>.Count);

        // load the constants into registers
        uint quotesConsumed = 0;
        ulong crCarry = 0;

        Vector512<byte> vecDelim = Vector512.Create(byte.CreateTruncating(_delimiter));
        Vector512<byte> vecQuote = Vector512.Create(byte.CreateTruncating(_quote));
        Vector512<byte> vecLF = Vector512.Create((byte)'\n');
        Vector512<byte> vecCR = TNewline.IsCRLF ? Vector512.Create((byte)'\r') : default;

        Vector512<byte> vector = AsciiVector.Load512(ref first, index);

        Vector512<byte> hasLF = Vector512.Equals(vector, vecLF);
        Vector512<byte> hasCR = TNewline.IsCRLF ? Vector512.Equals(vector, vecCR) : default;
        Vector512<byte> hasDelimiter = Vector512.Equals(vector, vecDelim);
        Vector512<byte> hasControl = hasLF | hasDelimiter;
        Vector512<byte> hasQuote = Vector512.Equals(vector, vecQuote);

        ulong maskCR = TNewline.IsCRLF ? AsciiVector.MoveMaskARM64(hasCR) : 0;
        ulong maskControl = AsciiVector.MoveMaskARM64(hasControl);
        ulong maskLF = AsciiVector.MoveMaskARM64(hasLF);
        ulong maskQuote = AsciiVector.MoveMaskARM64(hasQuote);

        Vector512<byte> nextVector = AsciiVector.Load512(ref first, index + (nuint)Vector512<byte>.Count);

        do
        {
            // Prefetch the vector that will be needed 2 iterations ahead
            Vector512<byte> prefetchVector = AsciiVector.Load512(ref first, index + (nuint)(2 * Vector512<byte>.Count));

            Unsafe.SkipInit(out ulong shiftedCR); // this can be garbage on LF, it's never used

            if (TNewline.IsCRLF)
            {
                vector = nextVector;
                nextVector = prefetchVector;

                shiftedCR = ((maskCR << 1) | crCarry);
                crCarry = maskCR >> 63;

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

            uint controlCount = (uint)BitOperations.PopCount(maskControl);

            if ((quotesConsumed | maskQuote) != 0)
            {
                goto SlowPath;
            }

            // if (Bithacks.ZeroOrOneBitsSet(maskLF))
            // {
            //     ParseDelimitersAndNewlines(
            //         count: controlCount,
            //         mask: maskControl,
            //         maskLF: maskLF,
            //         shiftedCR: shiftedCR,
            //         index: (uint)index,
            //         dst: ref Unsafe.Add(ref firstField, fieldIndex)
            //     );

            //     fieldIndex += controlCount;
            //     goto ContinueRead;
            // }

            SlowPath:
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

            ContinueRead:
            index += (nuint)Vector512<byte>.Count;

            hasLF = Vector512.Equals(vector, vecLF);
            hasCR = TNewline.IsCRLF ? Vector512.Equals(vector, vecCR) : default;
            hasDelimiter = Vector512.Equals(vector, vecDelim);
            hasQuote = Vector512.Equals(vector, vecQuote);
            hasControl = hasLF | hasDelimiter;

            maskCR = TNewline.IsCRLF ? AsciiVector.MoveMaskARM64(hasCR) : 0;
            maskControl = AsciiVector.MoveMaskARM64(hasControl);
            maskLF = AsciiVector.MoveMaskARM64(hasLF);
            maskQuote = AsciiVector.MoveMaskARM64(hasQuote);
        } while (fieldIndex <= fieldEnd && index <= searchSpaceEnd);

        return (int)fieldIndex;
    }

    private static bool IsZero(Vector512<byte> vector)
    {
        Vector256<byte> lower = vector.GetLower();
        Vector256<byte> upper = vector.GetUpper();
        Vector128<byte> a = lower.GetLower();
        Vector128<byte> b = lower.GetUpper();
        Vector128<byte> c = upper.GetLower();
        Vector128<byte> d = upper.GetUpper();

        return (a | b | c | d) == Vector128<byte>.Zero;
    }
}
