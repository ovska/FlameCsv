using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Readers.Internal;

/// <summary>
/// Wrapper around an array reference and the array pool that owns it.
/// </summary>
internal readonly ref struct ValueBufferOwner<T> where T : unmanaged
{
    private readonly Span<T[]?> _span;
    private readonly ArrayPool<T> _pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueBufferOwner(ref T[]? span, ArrayPool<T> pool)
    {
        _span = MemoryMarshal.CreateSpan(ref span, 1);
        _pool = pool;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int length)
    {
        ref T[]? array = ref _span[0];
        _pool.EnsureCapacity(ref array, length); // TODO: buffer clearing
        return array.AsSpan(0, length);
    }
}
