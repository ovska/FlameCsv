using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

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
    public abstract int MaxFieldsPerIteration { get; }

    /// <summary>
    /// Number of extra elements the tokenizer reads beyond the last processed chunk.
    /// </summary>
    protected abstract int Overscan { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int Tokenize(Span<uint> destination, int startIndex, ReadOnlySpan<T> data)
    {
        Debug.Assert(data.Length <= Field.MaxFieldEnd);
        Debug.Assert(destination.Length >= MaxFieldsPerIteration);

        if ((uint)(data.Length - startIndex) < Overscan)
        {
            return 0;
        }

        fixed (T* start = data)
        {
            T* end = start + data.Length - Overscan;
            return TokenizeCore(destination, startIndex, start, end);
        }
    }

    /// <summary>
    /// Reads fields from the data into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">Buffer to parse the records to</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="start">Pointer to the start of the data</param>
    /// <param name="end">Pointer to the end of the safe read range</param>
    /// <returns>Number of fields read</returns>
    protected abstract unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end);

    #region x86

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
            maskControl = Bithacks.ResetLowestSetBit(maskControl);

            uint eolFlag = Bithacks.ProcessFlag(maskLF, tz, flag);
            uint pos = index + tz;

            dst = pos - eolFlag;
            dst = ref Unsafe.Add(ref dst, 1);
        } while (maskControl != TMask.Zero);
    }

    /// <summary>
    /// Parses any control characters (LF, CR, delimiter).
    /// </summary>
    /// <param name="index">Index in the data</param>
    /// <param name="firstField">Reference where to write the fields to</param>
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

            dstField = pos | Bithacks.GetQuoteFlags(quotesConsumed);

            fIdx++;
            quotesConsumed = 0;
        }

        fieldIndex = fIdx;
    }

    #endregion x86

    #region ARM

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ParseAnyArm64(
        uint index,
        scoped ref uint firstField,
        scoped ref nuint fieldIndex,
        scoped ref uint quotesConsumed,
        uint maskControl,
        uint maskLF,
        scoped ref uint maskQuote,
        uint flag
    )
    {
        if (!ArmBase.Arm64.IsSupported)
            throw new PlatformNotSupportedException();

        uint consumed = 0;
        maskControl = ArmBase.ReverseElementBits(maskControl);

        Vector64<uint> popcnt;
        Vector64<byte> aggregated;

        nuint fIdx = fieldIndex;

        while (maskControl != 0)
        {
            uint lz = (uint)ArmBase.LeadingZeroCount(maskControl);
            uint quoteBits = (uint)((ulong)maskQuote << (int)(32u - lz));
            uint offset = consumed + lz;

            // interleave vector and scalar work
            popcnt = Vector64.CreateScalar(quoteBits);

            uint bitLF = maskLF >> (int)offset;

            aggregated = AdvSimd.Arm64.AddAcross(AdvSimd.PopCount(popcnt.AsByte()));

            uint eolFlag = flag & (0u - (bitLF & 1u));

            int k = (int)(lz + 1);
            uint result = index + offset - eolFlag;

            quotesConsumed += aggregated.ToScalar();

            // zero extend through ulong so shift by 32 works correctly
            maskControl = (uint)((ulong)maskControl << k);
            maskQuote = (uint)((ulong)maskQuote >> k);

            result |= Bithacks.GetQuoteFlags(quotesConsumed);

            consumed += (uint)k;
            quotesConsumed = 0;

            Unsafe.Add(ref firstField, fIdx++) = result;
        }

        fieldIndex = fIdx;
    }

    #endregion ARM

    #region Pathological

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void CheckDanglingCR<TMask>(
        scoped ref TMask maskControl,
        scoped ref T first,
        uint index,
        ref nuint fieldIndex,
        ref uint fieldRef,
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

            Unsafe.Add(ref fieldRef, fieldIndex) = (index - 1) | flag | Bithacks.GetQuoteFlags(quotesConsumed);

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

            Unsafe.Add(ref fieldRef, fieldIndex) = value | flag | Bithacks.GetQuoteFlags(quotesConsumed);
            quotesConsumed = 0;
            fieldIndex++;
        }

        quotesConsumed += uint.CreateTruncating(TMask.PopCount(maskQuote));
    }

    #endregion
}
