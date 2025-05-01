using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Represents a field in the CSV data.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
[SkipLocalsInit]
internal readonly struct Field
{
    internal const int MaxSpecialCount = 0x7FFF; // Maximum value for special count (15 bits)

    // Constants for bit masks
    private const int NeedsProcessingFlag = unchecked((int)0x80000000);
    private const int IsEscapeFlag = unchecked((int)0x8000);

    [FieldOffset(0)]
    private readonly int _startAndFlag; // 31 bits for start, 1 bit for needsProcessing

    [FieldOffset(4)]
    private readonly ushort _length; // 16 bits for length (up to 65,535)

    [FieldOffset(6)]
    private readonly ushort _specialCount; // 15 bits for count, 1 bit for isEscape

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field(int start, ushort length)
    {
        _startAndFlag = start;
        _length = length;
        _specialCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field(int start, ushort length, ushort specialCount, bool needsProcessing)
    {
        _startAndFlag = start | (needsProcessing ? NeedsProcessingFlag : 0);
        _length = length;
        _specialCount = specialCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field(int start, ushort length, ushort specialCount, bool isEscape, bool needsProcessing)
    {
        _startAndFlag = start | (needsProcessing ? NeedsProcessingFlag : 0);
        _length = length;
        _specialCount = (ushort)(specialCount | (isEscape ? IsEscapeFlag : 0));
    }

    // Properties for data access
    public int Start
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startAndFlag & ~NeedsProcessingFlag;
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    public bool NeedsProcessing
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_startAndFlag & NeedsProcessingFlag) != 0;
    }

    public bool IsEscape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_specialCount & IsEscapeFlag) != 0;
    }

    public int SpecialCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _specialCount & ~IsEscapeFlag;
    }

    public void GetRawSpan(out int start, out int length)
    {
        // Check bit 31 (NeedsProcessingFlag) and bytes 6-7 (_specialCount)
        const ulong mask = (1UL << 31) | (0xFFFFUL << 48); // won't work on Big Endian

        start = Start;
        length = Length;

        if ((Unsafe.BitCast<Field, ulong>(this) & mask) != 0)
        {
            start -= 1;
            length += 2;
        }
    }
}

internal sealed class FieldStack : IDisposable
{
    private ListBuilder<Range> _records;
    private ListBuilder<Field> _fields;

    public FieldStack(int capacity = 512)
    {
        _fields = new ListBuilder<Field>(capacity);
    }

    public void Reset()
    {
        _fields.Reset();
        _records.Reset();
    }

    public ReadOnlySpan<Range> Records
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _records.AsSpan();
    }

    public ReadOnlySpan<Field> Fields
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.AsSpan();
    }

    public void Process<T, TTrimmer>(ReadOnlySpan<Meta> fields, ReadOnlySpan<T> data, T quote)
        where T : unmanaged, IBinaryInteger<T>
        where TTrimmer : struct, ITrimmer
    {
        if (fields.IsEmpty)
            return;

        int fieldCount = fields.Length - 1;
        Span<Field> fieldSpan = _fields.ResetAndGetCapacitySpan(fieldCount);

        ref Meta meta = ref MemoryMarshal.GetReference(fields);
        ref Field field = ref MemoryMarshal.GetReference(fieldSpan);
        ref T first = ref MemoryMarshal.GetReference(data);

        nint metaIndex = 1;
        nint fieldIndex = 0;
        nint recordStart = 0;

        int start = meta.NextStart;

        while (metaIndex <= fieldCount)
        {
            Meta current = Unsafe.Add(ref meta, metaIndex);
            int length = current.End - start;

            // Safety check for length
            if ((uint)length > ushort.MaxValue)
                ThrowHelper.ThrowArgumentOutOfRange("Field length exceeds maximum allowed");

            int specialCount = (int)current.SpecialCount;
            bool isEscape = current.IsEscape;

            // Safety check for special count
            if (specialCount > Field.MaxSpecialCount)
                ThrowHelper.ThrowArgumentOutOfRange("Special count exceeds maximum allowed");

            ReadOnlySpan<T> span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, start), length);
            TTrimmer.Trim(ref span);

            if (current.HasControlCharacters)
            {
                if (Unsafe.Add(ref first, start) != quote || Unsafe.Add(ref first, start + length - 1) != quote)
                {
                    IndexOfUnescaper.Invalid(span, in current);
                }

                start++;
                length -= 2;

                Unsafe.Add(ref field, fieldIndex) = new Field(
                    start,
                    (ushort)length,
                    (ushort)specialCount,
                    isEscape,
                    needsProcessing: isEscape || specialCount != 2
                );
            }
            else
            {
                Unsafe.Add(ref field, fieldIndex) = new Field(start, (ushort)length);
            }

            start = current.NextStart;
            metaIndex++;
            fieldIndex++;

            if (current.IsEOL)
            {
                _records.Push(new Range((int)recordStart, (int)fieldIndex));
                recordStart = fieldIndex;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _fields.Dispose();
        _records.Dispose();
    }
}

file static class ThrowHelper
{
    public static void ThrowArgumentOutOfRange(string message)
    {
        throw new ArgumentOutOfRangeException(message: message, null);
    }
}
