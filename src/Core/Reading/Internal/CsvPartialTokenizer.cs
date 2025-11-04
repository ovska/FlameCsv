using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal abstract class CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Minimum length of data to get reasonably good performance.
    /// </summary>
    public abstract int PreferredLength { get; }

    /// <summary>
    /// Minimum length of the field buffer to safely read into,
    /// e.g. the number of fields that can be read per iteration without bounds checks.
    /// </summary>
    public abstract int MinimumFieldBufferSize { get; }

    /// <summary>
    /// Reads fields from the data into <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">Buffer to parse the records to</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="data">Data to read from</param>
    /// <returns>Number of fields read</returns>
    public abstract int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data);

    /// <summary>
    /// Parses any control characters (LF, CR, delimiter).
    /// </summary>
    /// <param name="index">Index in the data</param>
    /// <param name="firstField">Reference where to write the fields to</param>
    /// <param name="firstQuote">Reference where to write the quotes to</param>
    /// <param name="fieldIndex">Index of the current field</param>
    /// <param name="quotesConsumed">How many quotes are in the current field.</param>
    /// <param name="maskControl">Bitmask for all control characters</param>
    /// <param name="maskLF">Bitmask for LFs</param>
    /// <param name="maskQuote">Bitmask for quotes</param>
    /// <param name="flag">Flag to use for EOLs</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParseAny<TMask>(
        uint index,
        scoped ref uint firstField,
        scoped ref byte firstQuote,
        scoped ref nuint fieldIndex,
        scoped ref uint quotesConsumed,
        TMask maskControl,
        TMask maskLF,
        TMask maskQuote,
        uint flag
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        nuint fIdx = fieldIndex;

        if (ArmBase.Arm64.IsSupported)
        {
            Debug.Assert(typeof(TMask) == typeof(uint));

            maskControl = Unsafe.BitCast<uint, TMask>(
                ArmBase.ReverseElementBits(Unsafe.BitCast<TMask, uint>(maskControl))
            );

            while (maskControl != TMask.Zero)
            {
                uint tz = uint.CreateTruncating(TMask.LeadingZeroCount(maskControl));
                uint quoteBits = (uint)(ulong.CreateTruncating(maskQuote) << (int)(32 - tz));
                int k = (int)(tz + 1);

                uint eolFlag = Bithacks.ProcessFlag(maskLF, index + tz, flag);
                quotesConsumed += (uint)BitOperations.PopCount(quoteBits);

                Field.SaturateTo7Bits(ref quotesConsumed);

                ref uint dstField = ref Unsafe.Add(ref firstField, fIdx);
                ref byte dstQuote = ref Unsafe.Add(ref firstQuote, fIdx);

                dstField = index + tz - eolFlag;
                dstQuote = (byte)quotesConsumed;

                maskControl <<= k;
                maskQuote >>= k;

                index += (uint)k;
                fIdx++;
                quotesConsumed = 0;
            }
        }
        else
        {
            while (maskControl != TMask.Zero)
            {
                uint tz = uint.CreateTruncating(TMask.TrailingZeroCount(maskControl));
                TMask maskUpToPos = Bithacks.GetMaskUpToLowestSetBit(maskControl);
                TMask quoteBits = maskQuote & maskUpToPos;

                uint eolFlag = Bithacks.ProcessFlag(maskLF, tz, flag);
                uint pos = index + tz;
                quotesConsumed += uint.CreateTruncating(TMask.PopCount(quoteBits));

                pos -= eolFlag;

                Field.SaturateTo7Bits(ref quotesConsumed);

                // consume masks
                maskControl = Bithacks.ResetLowestSetBit(maskControl);
                maskQuote &= ~maskUpToPos;

                ref uint dstField = ref Unsafe.Add(ref firstField, fIdx);
                ref byte dstQuote = ref Unsafe.Add(ref firstQuote, fIdx);

                dstField = pos;
                dstQuote = (byte)quotesConsumed;

                fIdx++;
                quotesConsumed = 0;
            }
        }

        quotesConsumed += uint.CreateTruncating(TMask.PopCount(maskQuote));
        fieldIndex = fIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void CheckDanglingCR<TMask>(
        scoped ref TMask maskControl,
        scoped ref T first,
        uint index,
        ref nuint fieldIndex,
        ref uint fieldRef,
        ref byte quoteRef,
        scoped ref uint quotesConsumed
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        if (index == 0)
            return;

        ref T previous = ref Unsafe.Add(ref first, --index);

        if (previous == T.CreateTruncating('\r'))
        {
            uint flag;

            if (Unsafe.Add(ref previous, 1) == T.CreateTruncating('\n'))
            {
                flag = Field.IsCRLF;
                maskControl = Bithacks.ResetLowestSetBit(maskControl);
            }
            else
            {
                flag = Field.IsEOL;
            }

            Unsafe.Add(ref fieldRef, fieldIndex) = index | flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)Math.Min(quotesConsumed, 127);

            fieldIndex++;
            quotesConsumed = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParsePathological<TMask>(
        TMask maskControl,
        TMask maskQuote,
        scoped ref T first,
        uint index,
        ref nuint fieldIndex,
        ref uint fieldRef,
        ref byte quoteRef,
        T delimiter,
        scoped ref uint quotesConsumed
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        while (maskControl != TMask.Zero)
        {
            uint tz = uint.CreateTruncating(TMask.TrailingZeroCount(maskControl));
            TMask maskUpToPos = Bithacks.GetMaskUpToLowestSetBit(maskControl);

            uint value = index + tz;
            TMask quoteBits = maskQuote & maskUpToPos;

            ref T token = ref Unsafe.Add(ref first, value);
            uint flag = 0;

            quotesConsumed += uint.CreateTruncating(TMask.PopCount(quoteBits));

            if (delimiter != token)
            {
                if (Bithacks.IsCRLF(ref token))
                {
                    flag = Field.IsCRLF;
                    maskUpToPos = (maskUpToPos << 1) | TMask.One;
                }
                else
                {
                    flag = Field.IsEOL;
                }
            }

            maskControl &= ~maskUpToPos;
            maskQuote &= ~maskUpToPos;

            Unsafe.Add(ref fieldRef, fieldIndex) = value | flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)Math.Min(quotesConsumed, 127);
            quotesConsumed = 0;
            fieldIndex++;
        }

        quotesConsumed += uint.CreateTruncating(TMask.PopCount(maskQuote));
    }
}

file static class LUT
{
    public static ReadOnlySpan<uint> LowestBits =>
        [
            0b0000_0000_0000_0000_0000_0000_0000_0000,
            0b0000_0000_0000_0000_0000_0000_0000_0001,
            0b0000_0000_0000_0000_0000_0000_0000_0011,
            0b0000_0000_0000_0000_0000_0000_0000_0111,
            0b0000_0000_0000_0000_0000_0000_0000_1111,
            0b0000_0000_0000_0000_0000_0000_0001_1111,
            0b0000_0000_0000_0000_0000_0000_0011_1111,
            0b0000_0000_0000_0000_0000_0000_0111_1111,
            0b0000_0000_0000_0000_0000_0000_1111_1111,
            0b0000_0000_0000_0000_0000_0001_1111_1111,
            0b0000_0000_0000_0000_0000_0011_1111_1111,
            0b0000_0000_0000_0000_0000_0111_1111_1111,
            0b0000_0000_0000_0000_0000_1111_1111_1111,
            0b0000_0000_0000_0000_0001_1111_1111_1111,
            0b0000_0000_0000_0000_0011_1111_1111_1111,
            0b0000_0000_0000_0000_0111_1111_1111_1111,
            0b0000_0000_0000_0000_1111_1111_1111_1111,
            0b0000_0000_0000_0001_1111_1111_1111_1111,
            0b0000_0000_0000_0011_1111_1111_1111_1111,
            0b0000_0000_0000_0111_1111_1111_1111_1111,
            0b0000_0000_0000_1111_1111_1111_1111_1111,
            0b0000_0000_0001_1111_1111_1111_1111_1111,
            0b0000_0000_0011_1111_1111_1111_1111_1111,
            0b0000_0000_0111_1111_1111_1111_1111_1111,
            0b0000_0000_1111_1111_1111_1111_1111_1111,
            0b0000_0001_1111_1111_1111_1111_1111_1111,
            0b0000_0011_1111_1111_1111_1111_1111_1111,
            0b0000_0111_1111_1111_1111_1111_1111_1111,
            0b0000_1111_1111_1111_1111_1111_1111_1111,
            0b0001_1111_1111_1111_1111_1111_1111_1111,
            0b0011_1111_1111_1111_1111_1111_1111_1111,
            0b0111_1111_1111_1111_1111_1111_1111_1111,
            0b1111_1111_1111_1111_1111_1111_1111_1111,
            0b1111_1111_1111_1111_1111_1111_1111_1111,
        ];
}
