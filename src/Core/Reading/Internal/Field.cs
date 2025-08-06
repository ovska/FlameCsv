using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Reading.Internal;

internal static class Field
{
    /*
        00  - Delimiter
        10  - First field, or last field without a trailing delimiter
        01  - LF
        11  - CRLF
    */

    /// <summary>
    /// Flag for the start of the first record, or the end of the last record without a trailing newline.
    /// </summary>
    public const uint StartOrEnd = 1u << 31;

    /// <summary>
    /// Flag for EOL.
    /// </summary>
    public const uint IsEOL = 1u << 30;

    /// <summary>
    /// Flag for CRLF (two-character EOL)
    /// </summary>
    public const uint IsCRLF = 1u << 30 | 1u << 31;

    /// <summary>
    /// Mask for the end index of the field.
    /// </summary>
    public const uint EndMask = 0x3FFFFFFF;

    /// <summary>
    /// Mask for the EOL bits.
    /// </summary>
    public const uint FlagMask = ~EndMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStart(uint field)
    {
        // store the offsets in a small LUT; LF and delimiter have offset of 1, CRLF 2, and start/end 0
        uint end = field & EndMask;
        uint offset;

        // TODO: perf profile against a ReadOnlySpan LUT
        if (Bmi2.IsSupported)
        {
            offset = (uint)(0b_10_00_01_01 >> (int)((field >> 30) << 1)) & 3;
        }
        else
        {
            uint b = field >> 30;
            offset = ((b & (b >> 1)) << 1) | (1 ^ (b >> 1));
        }

        return (int)(end + offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int End(uint field) => (int)(field & EndMask);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlySpan<T> GetValue<T>(int start, uint field, byte quote, ref T data, CsvReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        int end = End(field);

        // trim before unquoting to preserve spaces in strings
        if (reader._dialect.Trimming != CsvFieldTrimming.None)
        {
            reader._dialect.Trimming.TrimUnsafe(ref data, ref start, ref end);
        }

        int length = end - start;

        ref T first = ref Unsafe.Add(ref data, (uint)start);
        ReadOnlySpan<T> retVal;

        if (IsEscape(quote))
        {
            goto EscapeOrInvalid;
        }

        if ((byte)(quote - 1) < 127)
        {
            T q = reader._dialect.Quote;

            if (length < 2 || first != q || Unsafe.Add(ref first, (uint)length - 1) != q)
            {
                goto EscapeOrInvalid;
            }

            retVal = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, 1), length - 2);

            if (quote != 2) // already trimmed the quotes
            {
                uint quoteCount = IsSaturated(quote)
                    ? (uint)System.MemoryExtensions.Count(retVal, q)
                    : (uint)(quote - 2); // TODO: make a quote agnostic unescaper?

                if (quoteCount % 2 != 0)
                {
                    goto EscapeOrInvalid;
                }

                Span<T> buffer = reader.GetUnescapeBuffer(retVal.Length);

                // Vector<char> is not supported
                if (Unsafe.SizeOf<T>() is sizeof(char))
                {
                    RFC4180Mode<ushort>.Unescape(
                        ushort.CreateTruncating(q),
                        buffer.Cast<T, ushort>(),
                        retVal.Cast<T, ushort>(),
                        quoteCount
                    );
                }
                else
                {
                    RFC4180Mode<T>.Unescape(q, buffer, retVal, quoteCount);
                }

                int unescapedLength = retVal.Length - unchecked((int)(quoteCount / 2));
                retVal = buffer.Slice(0, unescapedLength);
            }
        }
        else
        {
            retVal = MemoryMarshal.CreateReadOnlySpan(ref first, length);
        }

        return retVal;

        EscapeOrInvalid:
        return GetFieldNonStandard(start, field, quote, ref data, reader);
    }

    private static ReadOnlySpan<T> GetFieldNonStandard<T>(
        int start,
        uint field,
        byte quote,
        ref T data,
        CsvReader<T> reader
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        int length = End(field) - start;

        ReadOnlySpan<T> retVal = MemoryMarshal
            .CreateReadOnlySpan(ref Unsafe.Add(ref data, (uint)start), length)
            .Trim(reader._dialect.Trimming);

        if (
            retVal.Length < 2
            || retVal[^1] != reader._dialect.Quote
            || retVal[0] != reader._dialect.Quote
            || (!IsEscape(quote) && quote % 2 != 0)
        )
        {
            return IndexOfUnescaper.Invalid(retVal, field, quote);
        }

        uint specialCount = IsSaturated(quote)
            ? (uint)System.MemoryExtensions.Count(retVal, reader._dialect.Escape)
            : quote & 0x7Fu;

        Debug.Assert((quote & 0x80) != 0, $"Should be escape: {retVal.AsPrintableString()}");
        return IndexOfUnescaper.Unix(retVal[1..^1], reader, specialCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SaturateTo7Bits(ref uint quotesConsumed)
    {
        // this should be highly predictable so a branch is optimal
        if (quotesConsumed > 127)
        {
            quotesConsumed = 127;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEscape(byte quote) => (quote & 0x80) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSaturated(byte quote) => (quote & 0x7F) == 0x7F;
}
