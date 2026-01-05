using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Reads fields from the data into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">Buffer for the packed field bits</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="data">Data to tokenize</param>
    /// <returns>Number of fields read</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int Tokenize(Span<uint> destination, int startIndex, ReadOnlySpan<T> data)
    {
        Check.Positive(startIndex, "Start index cannot be negative");
        Check.GreaterThanOrEqual(destination.Length, MaxFieldsPerIteration, "Field buffer is too small");
        Check.LessThanOrEqual(data.Length, Field.MaxFieldEnd, "Data length exceeds 28 bits");

        if ((data.Length - startIndex) < Overscan)
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
    /// <param name="destination">Buffer for the packed field bits</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="start">Pointer to the start of the data</param>
    /// <param name="end">Pointer to the end of the safe read range</param>
    /// <returns>Number of fields read</returns>
    protected abstract unsafe int TokenizeCore(Span<uint> destination, int startIndex, T* start, T* end);

    /// <summary>
    /// Parses delimiters and newlines.
    /// </summary>
    /// <param name="index">Position of the mask in the data</param>
    /// <param name="dst">Packed field buffer to write fields to</param>
    /// <param name="maskControl">Composite mask of delimiters and newlines</param>
    /// <param name="maskLF">Mask of newline positions</param>
    /// <param name="flag">Flag to subtract from newline matches</param>
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
    /// Parses any control characters and quotes.
    /// </summary>
    /// <param name="index">Position of the mask in the data</param>
    /// <param name="firstField">Reference where to write the fields to</param>
    /// <param name="fieldIndex">Index of the current field</param>
    /// <param name="quotesConsumed">Running counter of previously read quotes</param>
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
            quotesConsumed += uint.CreateTruncating(TMask.PopCount(quoteBits));
            uint pos = index + tz;

            // consume masks
            maskControl = Bithacks.ResetLowestSetBit(maskControl);
            maskQuote &= ~maskUpToPos;

            pos -= eolFlag;

            ref uint dstField = ref Unsafe.Add(ref firstField, fIdx);

            dstField = pos | Bithacks.GetQuoteFlags(quotesConsumed);

            fIdx++;
            quotesConsumed = 0;
        }

        fieldIndex = fIdx;
    }
}
