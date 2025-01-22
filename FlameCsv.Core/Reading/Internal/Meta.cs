using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[StructLayout(LayoutKind.Explicit, Size = 8)]
internal readonly struct Meta
{
    private const int EOLMask = unchecked((int)0x80000000);
    private const int StartMask = unchecked((int)0x80000000);
    private const int IsEscapeMask = 0x40000000;
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
             - (isEOLMask * newlineLength); // negation of newline if EOL, 0 otherwise
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
        Debug.Assert(data.Length >= End - start);

        ReadOnlySpan<T> field = data[start..End];

        if (!dialect.Whitespace.IsEmpty)
        {
            field = field.Trim(dialect.Whitespace.Span);
        }

        if (_specialCountAndStart != 0)
        {
            uint specialCount = SpecialCount;

            if (field[0] != dialect.Quote || field[^1] != dialect.Quote)
            {
                Unescape.Invalid(field, in this);
            }

            field = field[1..^1];

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
    public static bool TryFindNextEOL(scoped ReadOnlySpan<Meta> meta, out int index)
    {
        index = 0;

        // TODO: vectorize me
        while (index < meta.Length)
        {
            if (meta[index++].IsEOL)
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
            if (IsStart) return $"Start {End}";
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
