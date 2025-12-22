using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal static partial class Field
{
    public const int MaxFieldEnd = (int)EndMask - 1;

    /*
        00  - Delimiter
        10  - EOL
        11  - CRLF
    */

    /// <summary>
    /// Flag for EOL.
    /// </summary>
    public const uint IsEOL = 1u << 31;

    /// <summary>
    /// Flag for CRLF (two-character EOL)
    /// </summary>
    public const uint IsCRLF = 0b11u << 30;

    public const uint IsQuoted = 1 << 29;

    public const uint NeedsUnescaping = 1 << 28;

    /// <summary>
    /// Mask for the end index of the field.
    /// </summary>
    public const uint EndMask = 0x0FFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStart(uint field) => (int)((field + 1) & EndMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStartCRLFAware(uint field) => (int)(((field + 1) & EndMask) + ((field >> 30) & 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int End(uint field) => (int)(field & EndMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsQuoting(uint field) => (int)(field << 2) < 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlySpan<T> GetValue<T>(int start, uint endAndMasks, ref T data, RecordOwner<T> owner)
        where T : unmanaged, IBinaryInteger<T>
    {
        int end = End(endAndMasks);

        if (owner._dialect.Trimming != 0)
        {
            owner._dialect.Trimming.TrimUnsafe(ref data, ref start, ref end);
        }

        int length = end - start;

        ref T first = ref Unsafe.Add(ref data, (uint)start);

        if ((int)(endAndMasks << 2) >= 0)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref first, length);
        }

        T quote = owner._dialect.Quote;

        if (length < 2 || first != quote || Unsafe.Add(ref first, (uint)length - 1u) != quote)
        {
            goto InvalidField;
        }

        ReadOnlySpan<T> fieldSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, 1u), length - 2);

        if ((int)(endAndMasks << 3) >= 0)
        {
            return fieldSpan;
        }

        Span<T> buffer = owner.GetUnescapeBuffer(fieldSpan.Length);
        int unescapedLength;

        // Vector<char> is not supported
        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            unescapedLength = Unescape(
                Unsafe.BitCast<T, ushort>(quote),
                Unsafe.BitCast<Span<T>, Span<ushort>>(buffer),
                Unsafe.BitCast<ReadOnlySpan<T>, ReadOnlySpan<ushort>>(fieldSpan)
            );
        }
        else
        {
            unescapedLength = Unescape(quote, buffer, fieldSpan);
        }

        Check.GreaterThanOrEqual(unescapedLength, 1);

        return MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(buffer), unescapedLength);

        InvalidField:
        return Invalid(start, end, ref data);
    }

    private static ReadOnlySpan<T> Invalid<T>(int start, int end, scoped ref T data)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<T> value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, (uint)start), end - start);

        string asString = value.AsPrintableString();

        throw new CsvFormatException($"Invalid quoted field {start}..{end}  with value: {asString}");
    }
}
