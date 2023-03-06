using System.Buffers;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Readers.Internal;

/// <summary>
/// Wrapper around an array reference and the array pool that owns it.
/// </summary>
internal readonly ref struct ValueBufferOwner<T> where T : unmanaged
{
    private readonly Span<T[]?> _span;
    private readonly ArrayPool<T> _pool;

    public ValueBufferOwner(ref T[]? span, ArrayPool<T>? pool)
    {
        _span = MemoryMarshal.CreateSpan(ref span, 1);
        _pool = pool ?? AllocatingArrayPool<T>.Instance;
    }

    public Span<T> GetSpan(int minimumLength)
    {
        ref T[]? array = ref _span[0];
        _pool.EnsureCapacity(ref array, minimumLength); // TODO: buffer clearing
        return array;
    }
}
