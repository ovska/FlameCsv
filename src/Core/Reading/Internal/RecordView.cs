using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal readonly struct RecordView
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordView(uint[] fields, byte[] quotes, uint index, int count)
    {
        _fields = fields;
        _quotes = quotes;
        _index = index;
        Count = count;
    }

    internal readonly uint[] _fields;
    internal readonly byte[] _quotes;

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
    public int GetLength(bool includeTrailingNewline = false)
    {
        return Fields.GetRecordLength(IsFirst, includeTrailingNewline);
    }

    public ReadOnlySpan<uint> Fields
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), Start),
                Count
            );
    }

    public ReadOnlySpan<byte> Quotes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_quotes), Start),
                Count
            );
    }
}
