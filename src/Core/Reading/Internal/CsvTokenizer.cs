using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class CsvTokenizer
{
    [ExcludeFromCodeCoverage]
    public static CsvTokenizer<T>? Create<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
#if NET10_0_OR_GREATER
        if (Avx512Tokenizer.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new Avx512Tokenizer<T, TrueConstant>(options)
                : new Avx512Tokenizer<T, FalseConstant>(options);
        }
#endif

        if (Avx2Tokenizer.IsSupported)
        {
            return options.Newline.IsCRLF()
                ? new Avx2Tokenizer<T, TrueConstant>(options)
                : new Avx2Tokenizer<T, FalseConstant>(options);
        }

        if (Vector128.IsHardwareAccelerated) // implies SSE, WASM or NEON
        {
            return options.Newline.IsCRLF()
                ? new SimdTokenizer<T, TrueConstant>(options)
                : new SimdTokenizer<T, FalseConstant>(options);
        }

        return null;
    }

    public static CsvScalarTokenizer<T> CreateScalar<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, TrueConstant>(options)
            : new ScalarTokenizer<T, FalseConstant>(options);
    }
}

[SkipLocalsInit]
internal abstract class CsvTokenizer<T>
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
    /// Reads fields from the data into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">Buffer to parse the records to</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="data">Data to read from</param>
    /// <returns>Number of fields read</returns>
    public abstract int Tokenize(FieldBuffer destination, int startIndex, ReadOnlySpan<T> data);

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
        scoped ref TMask maskQuote,
        uint flag
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        nuint fIdx = fieldIndex;

        while (maskControl != TMask.Zero)
        {
            uint tz = uint.CreateTruncating(TMask.TrailingZeroCount(maskControl));
            TMask maskUpToPos = Bithacks.GetMaskUpToLowestSetBit(maskControl);
            TMask quoteBits = maskQuote & maskUpToPos;

            uint eolFlag = Bithacks.ProcessFlag(maskLF, tz, flag);
            uint pos = index + tz;
            quotesConsumed += uint.CreateTruncating(TMask.PopCount(quoteBits));

            pos -= eolFlag;

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

        fieldIndex = fIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParseAnyArm64(
        uint index,
        scoped ref uint firstField,
        scoped ref byte firstQuote,
        scoped ref nuint fieldIndex,
        scoped ref uint quotesConsumed,
        uint maskControl,
        uint maskLF,
        scoped ref uint maskQuote,
        uint flag
    )
    {
        if (!ArmBase.Arm64.IsSupported)
            throw new UnreachableException();

        nuint fIdx = fieldIndex;

        uint consumed = 0;
        maskControl = ArmBase.ReverseElementBits(maskControl);

        Vector64<uint> popcnt;
        Vector64<byte> aggregated;

        while (maskControl != 0)
        {
            uint lz = (uint)ArmBase.LeadingZeroCount(maskControl);
            uint offset = consumed + lz;
            uint quoteBits = (uint)((ulong)maskQuote << (int)(32u - lz));

            // interleave vector and scalar work
            popcnt = Vector64.CreateScalar(quoteBits);

            uint bitLF = maskLF >> (int)offset;

            aggregated = AdvSimd.Arm64.AddAcross(AdvSimd.PopCount(popcnt.AsByte()));

            uint eolFlag = (bitLF & 1) != 0 ? flag : 0;
            int k = (int)(lz + 1);

            ref uint dstField = ref Unsafe.Add(ref firstField, fIdx);
            ref byte dstQuote = ref Unsafe.Add(ref firstQuote, fIdx);

            quotesConsumed += aggregated.ToScalar();

            dstField = index + offset - eolFlag;

            // zero extend through ulong so shift by 32 works correctly
            maskControl = (uint)((ulong)maskControl << k);
            maskQuote = (uint)((ulong)maskQuote >> k);

            dstQuote = (byte)quotesConsumed;

            consumed += (uint)k;
            fIdx++;
            quotesConsumed = 0;
        }

        fieldIndex = fIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParseControls<TMask>(
        uint index,
        scoped ref uint dst,
        TMask maskControl,
        TMask maskLF,
        uint flag
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        do
        {
            uint tz = uint.CreateTruncating(TMask.TrailingZeroCount(maskControl));

            uint eolFlag = Bithacks.ProcessFlag(maskLF, tz, flag);
            uint pos = index + tz;

            // consume masks
            maskControl = Bithacks.ResetLowestSetBit(maskControl);

            dst = pos - eolFlag;
            dst = ref Unsafe.Add(ref dst, 1);
        } while (maskControl != TMask.Zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParseControlsArm64(uint index, scoped ref uint dst, uint maskControl, uint maskLF, uint flag)
    {
        if (!ArmBase.Arm64.IsSupported)
            throw new UnreachableException();

        // reverse once; then we walk from MSB (bit 31) downward
        maskControl = ArmBase.ReverseElementBits(maskControl);
        maskLF = ArmBase.ReverseElementBits(maskLF);

        uint consumed = 0;

        while (maskControl != 0)
        {
            // next control at lz from current MSB
            uint lz = (uint)ArmBase.LeadingZeroCount(maskControl);

            // shift both masks so current control/newline land at bit 31
            uint lfShifted = maskLF << (int)lz;
            uint ctrlShifted = maskControl << (int)lz;

            uint offset = consumed + lz;

            // MSB set if newline
            uint eolFlag = (int)lfShifted < 0 ? flag : 0u;

            // store index (adjusted for CRLF flag if needed)
            dst = index + offset - eolFlag;
            dst = ref Unsafe.Add(ref dst, 1);

            // consume this control + everything above it by shifting once more
            maskControl = ctrlShifted << 1;
            maskLF = lfShifted << 1;

            // we advanced lz+1 positions total
            consumed = offset + 1;
        }
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

        ref T previous = ref Unsafe.Add(ref first, index - 1);

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

            Unsafe.Add(ref fieldRef, fieldIndex) = (index - 1) | flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)quotesConsumed;

            fieldIndex++;
            quotesConsumed = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParsePathological<TMask>(
        TMask maskControl,
        scoped ref TMask maskQuote,
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
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)quotesConsumed;
            quotesConsumed = 0;
            fieldIndex++;
        }

        quotesConsumed += uint.CreateTruncating(TMask.PopCount(maskQuote));
    }
}
