using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

        return options.Newline.IsCRLF()
            ? new SimdTokenizer<T, TrueConstant>(options)
            : new SimdTokenizer<T, FalseConstant>(options);
    }

    public static CsvScalarTokenizer<T> CreateScalar<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        return options.Newline.IsCRLF()
            ? new ScalarTokenizer<T, TrueConstant>(options)
            : new ScalarTokenizer<T, FalseConstant>(options);
    }
}

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
        TMask maskQuote,
        uint flag
    )
        where TMask : unmanaged, IBinaryInteger<TMask>
    {
        nuint fIdx = fieldIndex;

        if (ArmBase.Arm64.IsSupported)
        {
            Debug.Assert(typeof(TMask) == typeof(uint));

            uint consumed = 0;
            maskControl = Bithacks.ReverseBits(maskControl);

            while (maskControl != TMask.Zero)
            {
                uint lz = uint.CreateTruncating(TMask.LeadingZeroCount(maskControl));
                TMask quoteBits = Bithacks.IsolateLowestBits(maskQuote, lz);
                int k = (int)(lz + 1);

                uint eolFlag = Bithacks.ProcessFlag(maskLF, consumed + lz, flag);
                quotesConsumed += uint.CreateTruncating(TMask.PopCount(quoteBits));

                Field.SaturateQuotes(ref quotesConsumed);

                ref uint dstField = ref Unsafe.Add(ref firstField, fIdx);
                ref byte dstQuote = ref Unsafe.Add(ref firstQuote, fIdx);

                dstField = index + consumed + lz - eolFlag;
                dstQuote = (byte)quotesConsumed;

                // zero extend through ulong so shift by 32 works correctly
                maskControl = Unsafe.BitCast<uint, TMask>((uint)((ulong)Unsafe.BitCast<TMask, uint>(maskControl) << k));
                maskQuote = Unsafe.BitCast<uint, TMask>((uint)((ulong)Unsafe.BitCast<TMask, uint>(maskQuote) >> k));

                consumed += (uint)k;
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

                Field.SaturateQuotes(ref quotesConsumed);

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

            Field.SaturateQuotes(ref quotesConsumed);

            Unsafe.Add(ref fieldRef, fieldIndex) = (index - 1) | flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)quotesConsumed;

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

            Field.SaturateQuotes(ref quotesConsumed);

            Unsafe.Add(ref fieldRef, fieldIndex) = value | flag;
            Unsafe.Add(ref quoteRef, fieldIndex) = (byte)quotesConsumed;
            quotesConsumed = 0;
            fieldIndex++;
        }

        quotesConsumed += uint.CreateTruncating(TMask.PopCount(maskQuote));
    }
}
