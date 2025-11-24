using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.ParallelUtils;

internal sealed class ChunkManager<T>(T[] array, Action<T[]> release) : MemoryManager<T>, IConsumable
{
    public bool ShouldConsume => _index >= array.Length;

    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override Span<T> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_index < 0, this);
        return array.AsSpan(0, _index);
    }

    public override Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_index < 0, this);
            return new Memory<T>(array, 0, _index);
        }
    }

    protected override bool TryGetArray(out ArraySegment<T> segment)
    {
        ObjectDisposedException.ThrowIf(_index < 0, this);
        segment = new ArraySegment<T>(array, 0, _index);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        ObjectDisposedException.ThrowIf(_index < 0, this);
        Debug.Assert(_index < array.Length);
        array[_index++] = item;
    }

    public ArraySegment<T> UnsafeGetArray()
    {
        Debug.Assert(_index >= 0);
        return new ArraySegment<T>(array, 0, _index);
    }

    protected override void Dispose(bool disposing)
    {
        if (_index >= 0 && disposing)
        {
            _index = -1;
            release(array);
        }
    }

    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    public override void Unpin() => throw new NotSupportedException();
}
