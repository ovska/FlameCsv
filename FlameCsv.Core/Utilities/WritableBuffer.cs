using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Utilities;

internal struct WritableBuffer<T> : IDisposable where T : unmanaged
{
    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));
            return _items.Count;
        }
    }

    public readonly ReadOnlyMemory<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));
            return _array.AsMemory()[_items[index]];
        }
    }

    private readonly ArrayPool<T> _arrayPool;

    private Memory<T> _remaining;
    private T[] _array = [];

    private readonly List<Range> _items = [];

    public WritableBuffer(ArrayPool<T> arrayPool) => _arrayPool = arrayPool;

    public readonly void Clear()
    {
        ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));
        _items.Clear();
    }

    public void Push(ReadOnlyMemory<T> value)
    {
        ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));

        if (_remaining.Length < value.Length)
        {
            if (_array is null)
            {
                _array = _arrayPool.Rent(value.Length);
                _remaining = _array;
            }
            else
            {
                int written = _array.Length - _remaining.Length;
                _arrayPool.Resize(ref _array, _array.Length + value.Length);
                _remaining = _array.AsMemory(written);
            }
        }

        int start = _array.Length - _remaining.Length;

        value.CopyTo(_remaining);
        _remaining = _remaining.Slice(value.Length);
        _items.Add(new Range(start, start + value.Length));
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));
        _remaining = _array.AsMemory();
    }

    public void Dispose()
    {
        if (_array.Length > 0)
            _arrayPool.Return(_array);
        _array = null!;
        _remaining = default;
    }
}
