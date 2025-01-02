using System.Buffers;
using System.Diagnostics;
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
            return _array.AsMemory(range: _items[index]);
        }
    }

    private readonly ArrayPool<T> _arrayPool;

    private int _index;
    private T[] _array = [];

    private readonly List<Range> _items = [];

    public WritableBuffer(ArrayPool<T> arrayPool) => _arrayPool = arrayPool;

    public void Push(ReadOnlySpan<T> value)
    {
        ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));

        if ((_array.Length - _index) < value.Length)
        {
            if (_array is null)
            {
                Debug.Assert(_index == 0);
                _index = 0;
                _array = _arrayPool.Rent(Math.Max(value.Length, 256));
            }
            else
            {
                _arrayPool.Resize(ref _array, _array.Length + value.Length);
            }
        }

        int start = _index;

        value.CopyTo(_array.AsSpan(start));
        _index += value.Length;
        _items.Add(new Range(start, start + value.Length));
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_array is null, typeof(WritableBuffer<T>));
        _items.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        if (_array.Length > 0)
            _arrayPool.Return(_array);
        _array = null!;
        _index = 0;
    }
}
