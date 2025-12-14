using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal static partial class Field
{
    public const int MaxFieldEnd = (int)EndMask;

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

    /// <summary>
    /// Mask for the end index of the field.
    /// </summary>
    public const uint EndMask = 0x3FFFFFFF;

    public const ulong NeedsUnescapingMask = 1ul << 62;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStart(uint field)
    {
        // end offset result table:
        // 00 → 1 delimiter
        // 10 → 1 lf
        // 11 → 2 crlf

        // end offset is always 1, except in CRLF cases
        uint isCRLF = (field >> 30);
        uint end = (field & EndMask);
        uint bit = isCRLF & 1;
        return (int)(bit + (end + 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int End(uint field) => (int)(field & EndMask);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlySpan<T> GetValue<T>(int index, scoped CsvRecordRef<T> record)
        where T : unmanaged, IBinaryInteger<T>
    {
        ulong bits = Unsafe.Add(ref MemoryMarshal.GetReference(record._bits), (uint)index);
        scoped ref T data = ref record._data;
        RecordOwner<T> owner = record._owner;

        int start = (int)bits;
        int end = (int)(bits >> 32) & (int)EndMask;

        owner._dialect.Trimming.TrimUnsafe(ref data, ref start, ref end);

        int length = end - start;

        ref T first = ref Unsafe.Add(ref data, (uint)start);

        if ((long)bits >= 0)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref first, length);
        }

        T quote = owner._dialect.Quote;
        byte quoteCountByte;

        if (length < 2 || first != quote || Unsafe.Add(ref first, (uint)length - 1u) != quote)
        {
            quoteCountByte = 2;
            goto InvalidField;
        }

        ReadOnlySpan<T> fieldSpan = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, 1u), length - 2);

        if ((long)(bits << 1) >= 0)
        {
            return fieldSpan;
        }

        RecordBuffer recordBuffer = owner._recordBuffer;
        ref byte quoteRef = ref MemoryMarshal.GetArrayDataReference(recordBuffer._quotes);
        nint byteOffset = Unsafe.ByteOffset(
            in MemoryMarshal.GetArrayDataReference(recordBuffer._bits),
            in MemoryMarshal.GetReference(record._bits)
        );

        quoteCountByte = Unsafe.Add(ref quoteRef, (byteOffset / sizeof(ulong)) + index + 1);
        Debug.Assert(quoteCountByte >= 2, "Quote count should be at least 2 for quoted fields.");

        uint quoteCount = quoteCountByte == byte.MaxValue ? (uint)fieldSpan.Count(quote) : (quoteCountByte - 2u);

        if (quoteCount % 2 != 0)
        {
            goto InvalidField;
        }

        Span<T> buffer = owner.GetUnescapeBuffer(fieldSpan.Length);

        // Vector<char> is not supported
        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            Unescape(
                Unsafe.BitCast<T, ushort>(quote),
                Unsafe.BitCast<Span<T>, Span<ushort>>(buffer),
                Unsafe.BitCast<ReadOnlySpan<T>, ReadOnlySpan<ushort>>(fieldSpan),
                quoteCount
            );
        }
        else
        {
            Unescape(quote, buffer, fieldSpan, quoteCount);
        }

        int unescapedLength = fieldSpan.Length - (int)(quoteCount / 2);

        return buffer.Slice(0, unescapedLength);

        InvalidField:
        return Invalid(start, end, quoteCountByte, ref data);
    }

    private static ReadOnlySpan<T> Invalid<T>(int start, int end, byte quote, scoped ref T data)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<T> value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, (uint)start), end - start);

        string asString = value.AsPrintableString();

        throw new CsvFormatException($"Invalid quoted field {start}..{end} ({quote}) with value: {asString}");
    }
}
