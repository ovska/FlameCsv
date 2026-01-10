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
        Highest 2 bits encoding:
        00  - Delimiter
        10  - EOL
        11  - CRLF
        
        Next 2 bits:
        00  - No quoting
        10  - Only wrapping quotes
        11  - Needs unescaping
    */

    /// <summary>
    /// Flag for EOL.
    /// </summary>
    public const uint IsEOL = 1u << 31;

    /// <summary>
    /// Flag for CRLF (two-character EOL).
    /// </summary>
    public const uint IsCRLF = 0b11u << 30;

    /// <summary>
    /// Flag for quoted field (bit 29).
    /// </summary>
    public const uint IsQuotedMask = 1 << 29;

    /// <summary>
    /// Flag for field that needs unescaping (bit 28).
    /// </summary>
    public const uint NeedsUnescapingMask = IsQuotedMask | (1 << 28);

    /// <summary>
    /// Mask for the end index of the field (lowest 28 bits).
    /// </summary>
    public const uint EndMask = 0x0FFF_FFFF;

    /// <summary>Returns the start of the next field.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStart(uint field) => (int)((field + 1) & EndMask);

    /// <summary>Returns the start of the next field, taking into account a possible CRLF newline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextStartCRLFAware(uint field) => (int)(((field + 1) & EndMask) + ((field >> 30) & 1));

    /// <summary>Returns the end index from the packed field bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int End(uint field) => (int)(field & EndMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsQuoted(uint field) => (int)(field << 2) < 0; // move quote bit to MSB

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ReadOnlySpan<T> GetValue<T>(int start, uint endAndMasks, ref T data, RecordOwner<T> owner)
        where T : unmanaged, IBinaryInteger<T>
    {
        int end = End(endAndMasks);

        if (owner.Trimming != 0)
        {
            owner.Trimming.TrimUnsafe(ref data, ref start, ref end);
        }

        ref T first = ref Unsafe.Add(ref data, (uint)start);
        int length = end - start;

        // move quoting to MSB for fast check
        if ((int)(endAndMasks << 2) >= 0)
        {
            goto Done;
        }

        Check.NotEqual(owner.Quote, T.Zero, "Quote must be set if we reach this point!");
        Check.GreaterThanOrEqual(length, 2, "Can't have quoted field with length < 2");

        T quote = owner.Quote;

        if (first != quote || Unsafe.Add(ref first, (uint)length - 1u) != quote)
        {
            if (owner.AcceptInvalidQuotes)
            {
                goto Done;
            }

            return Invalid(start, end, ref data);
        }

        first = ref Unsafe.Add(ref first, 1u);
        length -= 2;

        if ((int)(endAndMasks << 3) >= 0)
        {
            goto Done;
        }

        Span<T> buffer = owner.GetUnescapeBuffer(length);
        int unescapedLength;

        // Vector<char> is not supported
        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            unescapedLength = Unescape(
                Unsafe.BitCast<T, ushort>(quote),
                Unsafe.BitCast<Span<T>, Span<ushort>>(buffer),
                MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, ushort>(ref first), length),
                owner.AcceptInvalidQuotes
            );
        }
        else
        {
            unescapedLength = Unescape(
                quote,
                buffer,
                MemoryMarshal.CreateReadOnlySpan(ref first, length),
                owner.AcceptInvalidQuotes
            );
        }

        if (unescapedLength >= 0)
        {
            // set the field start ref to point to the unescape buffer
            length = unescapedLength;
            first = ref MemoryMarshal.GetReference(buffer);
            Check.GreaterThanOrEqual(length, 1, "Unescaped length should be at least 1");
        }

        // otherwise, data was invalid but we are accepting invalid quotes

        Done:
        Check.GreaterThanOrEqual(length, 0, "Negative length?");
        return MemoryMarshal.CreateReadOnlySpan(ref first, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EnsureValid<T>(int start, uint endAndMasks, ref T data, RecordOwner<T> owner)
        where T : unmanaged, IBinaryInteger<T>
    {
        Check.True(IsQuoted(endAndMasks), "Field is not quoted; no need to validate.");
        Check.NotEqual(owner.Quote, T.Zero, "Quote must be set if we reach this point!");
        Check.False(owner.AcceptInvalidQuotes, "Cannot validate fields when AcceptInvalidQuotes is enabled.");

        int end = End(endAndMasks);

        if (owner.Trimming != 0)
        {
            owner.Trimming.TrimUnsafe(ref data, ref start, ref end);
        }

        ref T first = ref Unsafe.Add(ref data, (uint)start);
        int length = end - start;

        Check.GreaterThanOrEqual(length, 2, "Can't have quoted field with length < 2");

        T quote = owner.Quote;

        if (first != quote || Unsafe.Add(ref first, (uint)length - 1u) != quote)
        {
            Invalid(start, end, ref data);
        }

        // no inner quotes
        if ((int)(endAndMasks << 3) >= 0)
        {
            return;
        }

        first = ref Unsafe.Add(ref first, 1u);
        length -= 2;

        Check.GreaterThanOrEqual(length, 2, "Field with quotes must be at least 2 characters");

        if (typeof(T) == typeof(char))
        {
            ValidateQuotes(
                Unsafe.BitCast<T, ushort>(quote),
                MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, ushort>(ref first), length)
            );
        }
        else
        {
            ValidateQuotes(quote, MemoryMarshal.CreateReadOnlySpan(ref first, length));
        }
    }

    private static ReadOnlySpan<T> Invalid<T>(int start, int end, scoped ref T data)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<T> value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, (uint)start), end - start);
        string asString = value.AsPrintableString();
        throw new CsvFormatException($"Invalid quoted field {start}..{end} with value: {asString}");
    }
}
