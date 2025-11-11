using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal readonly struct RecordView
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordView(uint index, int count)
    {
        _index = index;
        Count = count;
    }

    // contains msb for "is first"
    private readonly uint _index;

    private const uint Mask = 0x7FFFFFFFu;

    public bool IsFirst
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((int)_index) < 0;
    }

    public int Start
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(_index & Mask);
    }

    public int Count { get; }

    public int FieldCount => Count - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetLength(RecordBuffer buffer, bool includeTrailingNewline = false)
    {
        return GetFields(buffer).GetRecordLength(IsFirst, includeTrailingNewline);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<uint> GetFields(RecordBuffer buffer)
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer._fields), Start),
            Count
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<byte> GetQuotes(RecordBuffer buffer)
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer._quotes), Start),
            Count
        );
    }
}
