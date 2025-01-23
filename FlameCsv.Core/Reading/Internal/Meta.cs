using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[StructLayout(LayoutKind.Explicit, Size = 8)]
internal readonly struct Meta
{
    /// <summary>
    /// Mask on <see cref="_endAndFlags"/> to check if the meta is at the end of the record.
    /// </summary>
    private const int EOLMask = unchecked((int)0x80000000);

    /// <summary>
    /// Mask on <see cref="_specialCountAndStart"/> to check if this is the start of data.
    /// </summary>
    private const int StartMask = unchecked((int)0x80000000);

    /// <summary>
    /// Mask on <see cref="_specialCountAndStart"/> to check if this meta is for a unix-style escaped meta.
    /// </summary>
    private const int IsEscapeMask = 0x40000000;

    /// <summary>
    /// Mask on <see cref="_specialCountAndStart"/> to extract the special count.
    /// </summary>
    private const int SpecialCountMask = 0x3FFF_FFFF; // 30 bits for special count

    [FieldOffset(0)] private readonly int _endAndFlags;

    [FieldOffset(4)] private readonly int _specialCountAndStart;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(int end, uint specialCount, bool isEscape, bool isEOL)
    {
        Debug.Assert(end >= 0);
        _endAndFlags = isEOL ? end | EOLMask : end;
        _specialCountAndStart = (int)(specialCount & SpecialCountMask | (uint)(isEscape ? IsEscapeMask : 0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(int endAndFlags, int startMask)
    {
        _endAndFlags = endAndFlags;
        _specialCountAndStart = startMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta RFC(int end, uint quoteCount, bool isEOL = false)
    {
        // ensure quote count is even and not too large
        if (((quoteCount & 1) | (quoteCount & ~SpecialCountMask)) != 0)
        {
            ThrowInvalidRFC(quoteCount, isEOL);
        }

        return new(end, quoteCount, isEscape: false, isEOL);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Plain(int end, bool isEOL = false)
        => new(endAndFlags: end | (isEOL ? EOLMask : 0), startMask: 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Unix(int end, uint quoteCount, uint escapeCount, bool isEOL = false)
    {
        // var quoteCountIsZeroOrTwo = (quoteCount & ~2u);
        // var escapeCountIsNonZero = (escapeCount - 1) >> 31;
        // var quoteCountIsNonZero = (quoteCount - 1) >> 31;
        // var escapeCountIsTooLarge = escapeCount & ~SpecialCountMask;
        // var quoteCountAndEscapeCountNonZero = (escapeCountIsNonZero & ~quoteCountIsNonZero);

        if ((quoteCount % 2 != 0) ||
            (escapeCount > 0 && quoteCount != 2) ||
            escapeCount > SpecialCountMask)
        {
            ThrowInvalidUnix(quoteCount, escapeCount, isEOL);
        }

        return new(end, escapeCount, isEscape: (quoteCount | escapeCount) != 0, isEOL);
    }

    public static Meta StartOfData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(endAndFlags: 0, startMask: StartMask);
    }

    public int End
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endAndFlags & ~EOLMask;
    }

    public bool IsEOL
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_endAndFlags & EOLMask) != 0;
    }

    public uint SpecialCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(_specialCountAndStart & SpecialCountMask);
    }

    public bool IsEscape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_specialCountAndStart & IsEscapeMask) != 0;
    }

    public bool IsStart
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_specialCountAndStart & StartMask) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetNextStart(int newlineLength)
    {
        int end = _endAndFlags & ~EOLMask;
        int isEOLMask = _endAndFlags >> 31;
        int isStartMask = _specialCountAndStart >> 31;
        //@formatter:off
        return 1 // delimiter
             + end // end index
             + isEOLMask // -1 if EOL, 0 otherwise (to clear the delimiter)
             + isStartMask // -1 if the start, 0 otherwise (to clear the delimiter)
             + (isEOLMask & newlineLength); // negation of newline if EOL, 0 otherwise
        //@formatter:on
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetField<T>(
        scoped ref readonly CsvDialect<T> dialect,
        int start,
        ReadOnlySpan<T> data,
        Span<T> buffer,
        CsvParser<T> parser)
        where T : unmanaged, IBinaryInteger<T>
    {
        // Preliminary testing with a small amount of real world data:
        // - 91.42% of fields have no quotes
        // - 8,51% of fields have just the wrapping quotes
        // - 0,08% of fields have quotes embedded, i.e. "John ""The Man"" Smith"
        // Optimizing the unescaping routine might not be worth it.

        Debug.Assert(data.Length >= End - start);

        if ((dialect.Whitespace.Length | (_specialCountAndStart & ~2)) == 0)
        {
            int offset = (_specialCountAndStart >> 31 | -_specialCountAndStart >> 31) & 1;
            int length = (_endAndFlags & ~EOLMask) - start;

            return MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(data), start + offset),
                length - offset - offset);
        }

        return GetFieldCore(in dialect, start, data, buffer, parser);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ReadOnlySpan<T> GetFieldCore<T>(
        scoped ref readonly CsvDialect<T> dialect,
        int start,
        ReadOnlySpan<T> data,
        Span<T> buffer,
        CsvParser<T> parser)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<T> field = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(data), (nint)(uint)start),
            (_endAndFlags & ~EOLMask) - start);

        // trim before unquoting to preserve whitespace in strings
        if (!dialect.Whitespace.IsEmpty)
        {
            field = field.Trim(dialect.Whitespace);
        }

        if (_specialCountAndStart != 0)
        {
            uint specialCount = SpecialCount;

            if (field.Length <= 1 || field[0] != dialect.Quote || field[^1] != dialect.Quote)
            {
                Unescape.Invalid(field, in this);
            }

            field = MemoryMarshal.CreateReadOnlySpan(ref field.DangerousGetReferenceAt(1), field.Length - 2);

            if (IsEscape && specialCount != 0)
            {
                var unescaper = new BackslashUnescaper<T>(dialect.Escape.GetValueOrDefault(), specialCount);
                int length = BackslashUnescaper<T>.UnescapedLength(field.Length, specialCount);

                if (length > buffer.Length)
                {
                    buffer = parser.GetUnescapeBuffer(length);
                }

                Unescape.Field(field, unescaper, buffer);
                field = buffer.Slice(0, length);
            }
            else if (!IsEscape && specialCount != 2) // already trimmed the quotes
            {
                var unescaper = new DoubleQuoteUnescaper<T>(dialect.Quote, specialCount - 2);
                int length = DoubleQuoteUnescaper<T>.UnescapedLength(field.Length, specialCount - 2);

                if (length > buffer.Length)
                {
                    buffer = parser.GetUnescapeBuffer(length);
                }

                Unescape.Field(field, unescaper, buffer);
                field = buffer.Slice(0, length);
            }
        }

        return field;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFindNextEOL(
        scoped ref Meta first,
        int end,
        out int index)
    {
        index = 0;
        int unrolledEnd = end - 8;

        while (index < unrolledEnd)
        {
            if ((Unsafe.Add(ref first, index)._endAndFlags & EOLMask) != 0)
            {
                index += 1;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 1)._endAndFlags & EOLMask) != 0)
            {
                index += 2;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 2)._endAndFlags & EOLMask) != 0)
            {
                index += 3;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 3)._endAndFlags & EOLMask) != 0)
            {
                index += 4;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 4)._endAndFlags & EOLMask) != 0)
            {
                index += 5;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 5)._endAndFlags & EOLMask) != 0)
            {
                index += 6;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 6)._endAndFlags & EOLMask) != 0)
            {
                index += 7;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 7)._endAndFlags & EOLMask) != 0)
            {
                index += 8;
                return true;
            }

            index += 8;
        }

        while (index < end)
        {
            if (Unsafe.Add(ref first, index++).IsEOL)
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasEOL(scoped ReadOnlySpan<Meta> meta, out int lastIndex)
    {
        lastIndex = meta.Length - 1;

        // TODO: vectorize me
        while (lastIndex >= 0)
        {
            if (meta[lastIndex].IsEOL)
            {
                return true;
            }

            lastIndex--;
        }

        return false;
    }

#if DEBUG
    static Meta()
    {
        if (Unsafe.SizeOf<Meta>() != 8) throw new UnreachableException("Meta must be 8 bytes in size");
    }
#endif

    internal string DebuggerDisplay
    {
        get
        {
            if (IsStart) return $"Start: {End}";
            return $"End: {End}, IsEOL: {IsEOL}, SpecialCount: {SpecialCount}, IsEscape: {IsEscape}";
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidRFC(uint quoteCount, bool isEOL)
    {
        string info = isEOL ? " at EOL" : "";

        if (quoteCount > SpecialCountMask)
        {
            throw new NotSupportedException(
                $"Csv field had too many quotes ({quoteCount}){info}, up to {SpecialCountMask} supported.");
        }

        throw new CsvFormatException($"Invalid CSV field{info}, unbalanced quotes ({quoteCount}).");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidUnix(uint quoteCount, uint escapeCount, bool isEOL)
    {
        string info = isEOL ? " at EOL" : "";

        if (escapeCount > SpecialCountMask)
        {
            throw new NotSupportedException(
                $"Csv field had too many escapes ({escapeCount}){info}, up to {SpecialCountMask} supported.");
        }

        throw new CsvFormatException($"Invalid CSV field{info}, unbalanced quotes ({quoteCount}).");
    }
}
