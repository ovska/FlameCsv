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

    [FieldOffset(0)] private readonly int _endAndEol;
    [FieldOffset(4)] private readonly int _specialCountAndOffset;

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

        return Unsafe.BitCast<long, Meta>(
            (uint)end | // end position
            ((long)quoteCount << 35) | // quote count
            (1L << 32)); // delimiter
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
            (uint)end | // end position
            unchecked((uint)EOLMask) |
            (((long)(uint)newlineLength) << 32) |
            ((long)quoteCount << 35));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta RFC(int end, uint quoteCount, bool isEOL, int newlineLength)
    {
        // ensure quote count is even and not too large
        if ((quoteCount & (1 | ~MaxSpecialCount)) != 0)
        {
            ThrowInvalidRFC(quoteCount, isEOL);
        }

        long mask = unchecked((uint)EOLMask) | (((long)(uint)newlineLength) << 32);
        long isEolMask = isEOL.ToBitwiseMask64();
        return Unsafe.BitCast<long, Meta>(
            (uint)end | // end position
            ((long)quoteCount << 35) | // quote count
            ((1L << 32) & ~isEolMask) | // delimiter, zero if EOL
            (mask & isEolMask)); // newline length + EOLMask, or zero if not EOL
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
            (uint)end |
            ((1L << 32) & ~isEolMask) |
            ((unchecked((uint)EOLMask) | (((long)(uint)newlineLength) << 32)) & isEolMask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Meta Unix(int end, uint quoteCount, uint escapeCount, bool isEOL, int newlineLength)
    {
        if ((quoteCount % 2 != 0) ||
            (escapeCount > 0 && quoteCount != 2) ||
            escapeCount > MaxSpecialCount)
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
    /// If <see langword="true"/>, <see cref="SpecialCount"/> refers to escape characters.
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
    public ReadOnlySpan<T> GetField<T>(
        scoped ref readonly CsvDialect<T> dialect,
        int start,
        scoped ref T data,
        Span<T> buffer,
        Allocator<T> allocator)
        where T : unmanaged, IBinaryInteger<T>
    {
        // don't touch this method without thorough benchmarking

        // Preliminary testing with a small amount of real world data:
        // - 91.42% of fields have no quotes
        // - 8,51% of fields have just the wrapping quotes
        // - 0,08% of fields have quotes embedded, i.e. "John ""The Man"" Smith"

        if (dialect.Trimming == CsvFieldTrimming.None)
        {
            // most common case, no quotes or escapes
            if ((_specialCountAndOffset & SpecialCountMask) == 0)
            {
                return MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.Add(ref data, start),
                    (_endAndEol & ~EOLMask) - start);
            }

            int length = (_endAndEol & ~EOLMask) - start;

            // check if the field is just wrapped in quotes; by doing both the quote count and quote checks at the same time,
            // the CPU can do both checks in parallel, and the branch predictor can predict the outcome of both checks
            // if quotes don't wrap the value (never happens in valid csv), refer to the slower routine that throws an exception
            // at this point, the field is guaranteed not to be empty so we can safely access the first and last element
            // escape bit:    0b00x00
            // newline bits:  0b000xx
            // 2 in binary:   0b10
            // therefore
            // special count: 0b10000

            T quote = dialect.Quote;

            if ((_specialCountAndOffset & (~0b11 ^ 0b10000)) == 0 &&
                quote == Unsafe.Add(ref data, start) &&
                quote == Unsafe.Add(ref data, start + length - 1))
            {
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, start + 1), length - 2);
            }
        }

        return GetFieldSlow(in dialect, start, ref data, buffer, allocator);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ReadOnlySpan<T> GetFieldSlow<T>(
        scoped ref readonly CsvDialect<T> dialect,
        int start,
        scoped ref T data,
        Span<T> buffer,
        Allocator<T> allocator)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<T> field = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref data, start), End - start);

        // trim before unquoting to preserve spaces in strings
        field = field.Trim(dialect.Trimming);

        if ((_specialCountAndOffset & (IsEscapeMask | SpecialCountMask)) != 0)
        {
            uint specialCount = SpecialCount;

            if (field.Length <= 1 || field[0] != dialect.Quote || field[^1] != dialect.Quote)
            {
                IndexOfUnescaper.Invalid(field, in this);
            }

            field = field[1..^1];

            if (IsEscape && specialCount != 0)
            {
                var unescaper = new IndexOfUnixUnescaper<T>(dialect.Escape.GetValueOrDefault(), specialCount);
                int length = IndexOfUnixUnescaper<T>.UnescapedLength(field.Length, specialCount);

                if (length > buffer.Length)
                {
                    buffer = allocator.GetSpan(length);
                }

                IndexOfUnescaper.Field(field, unescaper, buffer);
                field = buffer.Slice(0, length);
            }
            else if (!IsEscape && specialCount != 2) // already trimmed the quotes
            {
                if (field.Length > buffer.Length)
                {
                    buffer = allocator.GetSpan(field.Length);
                }

                if (typeof(T) == typeof(char))
                {
                    RFC4180Mode<ushort>.Unescape(
                        ushort.CreateTruncating(dialect.Quote),
                        buffer.Cast<T, ushort>(),
                        field.Cast<T, ushort>(),
                        specialCount - 2);
                }
                else
                {
                    RFC4180Mode<T>.Unescape(
                        dialect.Quote,
                        buffer,
                        field,
                        specialCount - 2);
                }

                int unescapedLength = field.Length - unchecked((int)((specialCount - 2) / 2));
                field = buffer.Slice(0, unescapedLength);
            }
        }

        return field;
    }

    /// <summary>
    /// Returns the index of the first EOL meta in the data.
    /// </summary>
    /// <param name="first">Reference to the first item in the search space</param>
    /// <param name="end">Number of items in the search space</param>
    /// <param name="index">Index of the first EOL</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFindNextEOL(
        scoped ref Meta first,
        int end,
        out int index)
    {
        index = 0;
        int unrolledEnd = end - 8;

        // jit optimizes the checks to (ulong)meta & (1UL << 31) != 0

        while (index < unrolledEnd)
        {
            if ((Unsafe.Add(ref first, index)._endAndEol & EOLMask) != 0)
            {
                index += 1;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 1)._endAndEol & EOLMask) != 0)
            {
                index += 2;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 2)._endAndEol & EOLMask) != 0)
            {
                index += 3;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 3)._endAndEol & EOLMask) != 0)
            {
                index += 4;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 4)._endAndEol & EOLMask) != 0)
            {
                index += 5;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 5)._endAndEol & EOLMask) != 0)
            {
                index += 6;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 6)._endAndEol & EOLMask) != 0)
            {
                index += 7;
                return true;
            }

            if ((Unsafe.Add(ref first, index + 7)._endAndEol & EOLMask) != 0)
            {
                index += 8;
                return true;
            }

            index += 8;
        }

        while (index < end)
        {
            if ((Unsafe.Add(ref first, index++)._endAndEol & EOLMask) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the span has an EOL field in it, and returns the last index if one is found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasEOL(scoped ReadOnlySpan<Meta> meta)
    {
        nint index = meta.Length - 1;
        ref Meta first = ref MemoryMarshal.GetReference(meta);

        while (index >= 8)
        {
            if (((Unsafe.Add(ref first, index - 0)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 1)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 2)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 3)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 4)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 5)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 6)._endAndEol & EOLMask) != 0) ||
                ((Unsafe.Add(ref first, index - 7)._endAndEol & EOLMask) != 0))
            {
                return true;
            }

            index -= 8;
        }

        while (index >= 0)
        {
            if ((Unsafe.Add(ref first, index)._endAndEol & EOLMask) != 0)
            {
                return true;
            }

            index--;
        }

        return false;
    }

    /// <summary>
    /// Shifts the end of the fields in the span by the given offset.
    /// </summary>
    /// <param name="meta"></param>
    /// <param name="offset"></param>
    public static void Shift(scoped Span<Meta> meta, int offset)
    {
        foreach (ref var m in meta)
        {
#if DEBUG
            Debug.Assert(m != StartOfData);
            Meta orig = m;
#endif

            // Preserve the EOL flag while shifting only the end position
            int eolFlag = m._endAndEol & EOLMask;
            Unsafe.AsRef(in m._endAndEol) = (m._endAndEol & ~EOLMask) - offset | eolFlag;

#if DEBUG
            Debug.Assert(orig.End == (offset + m.End));
            Debug.Assert(orig.IsEOL == m.IsEOL);
#endif
        }
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
            return
                $"{{ End: {End}, IsEOL: {IsEOL}, SpecialCount: {SpecialCount}, IsEscape: {IsEscape}, Offset: {NextStart - End}, Next: {NextStart} }}";
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
                $"Csv field had too many quotes ({quoteCount}){info}, up to {SpecialCountMask} supported.");
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
                $"Csv field had too many escapes ({escapeCount}){info}, up to {SpecialCountMask} supported.");
        }

        throw new CsvFormatException($"Invalid CSV field{info}, unbalanced quotes ({quoteCount}).");
    }

    public bool Equals(Meta other) => Unsafe.BitCast<Meta, long>(this) == Unsafe.BitCast<Meta, long>(other);
    public override bool Equals(object? obj) => obj is Meta other && Equals(other);
    public override int GetHashCode() => Unsafe.BitCast<Meta, long>(this).GetHashCode();
    public static bool operator ==(Meta left, Meta right) => left.Equals(right);
    public static bool operator !=(Meta left, Meta right) => !(left == right);
}
