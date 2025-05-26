using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[StructLayout(LayoutKind.Explicit, Size = 8)]
internal readonly struct Meta : IEquatable<Meta>
{
    /// <summary>
    /// Mask on <see cref="_endAndEol"/> to check if the field is followed by a newline.
    /// </summary>
    public const int EOLMask = unchecked((int)0x80000000);

    /// <summary>
    /// Mask on <see cref="_specialCountAndOffset"/> that contains the offset to the next field.
    /// </summary>
    public const int EndOffsetMask = 0b11;

    /// <summary>
    /// Mask on <see cref="_specialCountAndOffset"/> to check if special count refers to escapes.
    /// </summary>
    public const int IsEscapeMask = 0b100;

    /// <summary>
    /// Mask on <see cref="_specialCountAndOffset"/> to extract the special count.
    /// </summary>
    public const int SpecialCountMask = ~0b111;

    /// <summary>
    /// 29 bits reserved for the special count.
    /// </summary>
    public const uint MaxSpecialCount = unchecked(((uint)SpecialCountMask >> 3));

    [FieldOffset(0)]
    internal readonly int _endAndEol;

    [FieldOffset(4)]
    internal readonly int _specialCountAndOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(int end, uint specialCount, bool isEscape, bool isEOL, int newlineLength)
    {
        Debug.Assert(end >= 0);
        _endAndEol = end;
        _specialCountAndOffset = (int)(specialCount << 3);

        if (isEscape)
        {
            _specialCountAndOffset |= IsEscapeMask;
        }

        if (isEOL)
        {
            _endAndEol |= EOLMask;
            _specialCountAndOffset |= newlineLength;
        }
        else
        {
            _specialCountAndOffset |= 1; // delimiter
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta RFC(int end, uint quoteCount)
    {
        // ensure quote count is even and not too large
        if ((quoteCount & (1 | ~MaxSpecialCount)) != 0)
        {
            ThrowInvalidRFC(quoteCount, false);
        }

        return Unsafe.BitCast<long, Meta>((uint)end | ((long)quoteCount << 35) | (1L << 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta EOL(int end, uint quoteCount, int newlineLength)
    {
        // ensure quote count is even and not too large
        if ((quoteCount & (1 | ~MaxSpecialCount)) != 0)
        {
            ThrowInvalidRFC(quoteCount, false);
        }

        return Unsafe.BitCast<long, Meta>(
            (uint)end | unchecked((uint)EOLMask) | (((long)(uint)newlineLength) << 32) | ((long)quoteCount << 35)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta RFC(int end, uint quoteCount, bool isEOL, int newlineLength)
    {
        // ensure quote count is even and not too large
        if ((quoteCount & (1 | ~MaxSpecialCount)) != 0)
        {
            ThrowInvalidRFC(quoteCount, isEOL);
        }

        long newlineMask = unchecked((uint)EOLMask) | (((long)(uint)newlineLength) << 32);
        long isEolMask = isEOL.ToBitwiseMask64();
        return Unsafe.BitCast<long, Meta>(
            (uint)end | ((long)quoteCount << 35) | ((1L << 32) & ~isEolMask) | (newlineMask & isEolMask)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Plain(int end)
    {
        // no EOL, no special characters, 1 offset for the delimiter
        return Unsafe.BitCast<long, Meta>((uint)end | (1L << 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Plain(int end, bool isEOL, int newlineLength)
    {
        // splitting into variable is 3 bytes more code size, but better perf
        long isEolMask = isEOL.ToBitwiseMask64();
        return Unsafe.BitCast<long, Meta>(
            (uint)end
                | ((1L << 32) & ~isEolMask)
                | ((unchecked((uint)EOLMask) | (((long)(uint)newlineLength) << 32)) & isEolMask)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Unix(int end, uint quoteCount, uint escapeCount, bool isEOL, int newlineLength)
    {
        if ((quoteCount % 2 != 0) || (escapeCount > 0 && quoteCount != 2) || escapeCount > MaxSpecialCount)
        {
            ThrowInvalidUnix(quoteCount, escapeCount, isEOL);
        }

        // if escapeCount is 0, and we only have quotes, leave isEscape flag off
        return new(end, escapeCount == 0 ? quoteCount : escapeCount, isEscape: escapeCount != 0, isEOL, newlineLength);
    }

    /// <summary>
    /// Returns a meta at the start of the data (<see cref="NextStart"/> is 0).
    /// </summary>
    public static Meta StartOfData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    /// <summary>
    /// Returns the end index of the field.
    /// </summary>
    public int End
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _endAndEol & ~EOLMask;
    }

    /// <summary>
    /// Returns whether the field is followed by a newline.
    /// </summary>
    public bool IsEOL
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_endAndEol & EOLMask) != 0;
    }

    /// <summary>
    /// Returns the number of quotes or escapes in the field.
    /// </summary>
    public uint SpecialCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(_specialCountAndOffset & SpecialCountMask) >> 3;
    }

    /// <summary>
    /// If <c>true</c>, <see cref="SpecialCount"/> refers to escape characters.
    /// </summary>
    public bool IsEscape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_specialCountAndOffset & IsEscapeMask) != 0;
    }

    /// <summary>
    /// Returns the start of the next field.
    /// </summary>
    public int NextStart
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_endAndEol & ~EOLMask) + (_specialCountAndOffset & EndOffsetMask);
    }

    /// <summary>
    /// Offset to the next field.
    /// </summary>
    public int EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _specialCountAndOffset & EndOffsetMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetField<T>(int start, scoped ref T data, CsvReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        Dialect<T> dialect = reader._dialect;
        int length = (_endAndEol & ~EOLMask) - start;

        // Fast path for plain fields (91.42% case)
        if ((_specialCountAndOffset & SpecialCountMask) == 0 && dialect.Trimming == CsvFieldTrimming.None)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, start), length);
        }

        // Check for simple quoted fields (8.51% case)
        if (dialect.Trimming == CsvFieldTrimming.None && (_specialCountAndOffset & (~0b11 ^ 0b10000)) == 0)
        {
            ref T first = ref Unsafe.Add(ref data, start);
            T quote = dialect.Quote;

            // Prefetch these values to help branch prediction
            T firstChar = first;
            T lastChar = Unsafe.Add(ref first, length - 1);

            if (quote == firstChar && quote == lastChar)
            {
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, 1), length - 2);
            }
        }

        // Complex case (0.08% case) - separate method to reduce register pressure
        return GetFieldSlow(start, ref data, reader);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ReadOnlySpan<T> GetFieldSlow<T>(int start, scoped ref T data, CsvReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        Dialect<T> dialect = reader._dialect;

        int fieldLength = End - start;
        ReadOnlySpan<T> field = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, start), End - start);

        // trim before unquoting to preserve spaces in strings
        if (field.NeedsTrimming(dialect.Trimming))
        {
            field = field.Trim(dialect.Trimming);
        }

        if ((_specialCountAndOffset & (IsEscapeMask | SpecialCountMask)) != 0)
        {
            T quote = dialect.Quote;
            uint specialCount = SpecialCount;

            if (
                fieldLength <= 1
                || Unsafe.Add(ref data, start) != quote
                || Unsafe.Add(ref data, start + fieldLength - 1) != quote
            )
            {
                IndexOfUnescaper.Invalid(field, in this);
            }

            field = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, start + 1), fieldLength - 2);

            // for escapes, special count refers to the number of escape characters
            // for RFC, special count refers to the _total_ number of quotes, including wrapping
            if (IsEscape && specialCount != 0)
            {
                Debug.Assert(dialect.Escape != default, "Escape character is not set");
                var unescaper = new IndexOfUnixUnescaper<T>(dialect.Escape, specialCount);
                int length = IndexOfUnixUnescaper<T>.UnescapedLength(field.Length, specialCount);
                Span<T> buffer = reader.GetUnescapeBuffer(length);
                IndexOfUnescaper.Field(field, unescaper, buffer);
                field = buffer.Slice(0, length);
            }
            else if (!IsEscape && specialCount != 2) // already trimmed the quotes
            {
                Span<T> buffer = reader.GetUnescapeBuffer(fieldLength - 2);

                // Vector<char> is not supported
                if (Unsafe.SizeOf<T>() is sizeof(char))
                {
                    RFC4180Mode<ushort>.Unescape(
                        ushort.CreateTruncating(dialect.Quote),
                        buffer.Cast<T, ushort>(),
                        field.Cast<T, ushort>(),
                        specialCount - 2
                    );
                }
                else
                {
                    RFC4180Mode<T>.Unescape(dialect.Quote, buffer, field, specialCount - 2);
                }

                int unescapedLength = field.Length - unchecked((int)((specialCount - 2) / 2));
                field = buffer.Slice(0, unescapedLength);
            }
        }

        return field;
    }

#if DEBUG
    static Meta()
    {
        if (Unsafe.SizeOf<Meta>() != sizeof(ulong))
            throw new UnreachableException("Meta must be 8 bytes in size");
    }
#endif

    public override string ToString() => DebuggerDisplay;

    internal string DebuggerDisplay
    {
        get
        {
            if (this.Equals(default))
                return "{ Start: 0 }";
            return $"{{ End: {End}, IsEOL: {IsEOL}, SpecialCount: {SpecialCount}, IsEscape: {IsEscape}, Offset: {NextStart - End}, Next: {NextStart} }}";
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidRFC(uint quoteCount, bool isEOL)
    {
        string info = isEOL ? " at EOL" : "";

        if (quoteCount > MaxSpecialCount)
        {
            throw new NotSupportedException(
                $"Csv field had too many quotes ({quoteCount}){info}, up to {SpecialCountMask} supported."
            );
        }

        throw new CsvFormatException($"Invalid CSV field{info}, unbalanced quotes ({quoteCount}).");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidUnix(uint quoteCount, uint escapeCount, bool isEOL)
    {
        string info = isEOL ? " at EOL" : "";

        if (escapeCount > MaxSpecialCount)
        {
            throw new NotSupportedException(
                $"Csv field had too many escapes ({escapeCount}){info}, up to {SpecialCountMask} supported."
            );
        }

        throw new CsvFormatException($"Invalid CSV field{info}, unbalanced quotes ({quoteCount}).");
    }

    public bool Equals(Meta other) => Unsafe.BitCast<Meta, long>(this) == Unsafe.BitCast<Meta, long>(other);

    public override bool Equals(object? obj) => obj is Meta other && Equals(other);

    public override int GetHashCode() => Unsafe.BitCast<Meta, long>(this).GetHashCode();

    public static bool operator ==(Meta left, Meta right) => left.Equals(right);

    public static bool operator !=(Meta left, Meta right) => !(left == right);
}
