using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

internal readonly struct RecordView
{
    public RecordView(uint[] fields, byte[] quotes, int index, int count)
    {
        _fields = fields;
        _quotes = quotes;
        Start = index;
        Count = count;
    }

    internal readonly uint[] _fields;
    internal readonly byte[] _quotes;
    public int Start { get; }
    public int Count { get; }

    public int FieldCount => Count - 1;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<uint> GetFieldsForRef()
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), Start + 1),
            Count - 1
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetQuotesForRef()
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_quotes), Start + 1),
            Count - 1
        );
    }
}
