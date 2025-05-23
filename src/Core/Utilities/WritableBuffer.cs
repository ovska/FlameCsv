﻿using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.IO.Internal;

namespace FlameCsv.Utilities;

[SuppressMessage("CodeQuality", "IDE0064:Make readonly fields writable")]
internal struct WritableBuffer<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));
            return _items.Count;
        }
    }

    public readonly ReadOnlyMemory<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));
            Range range = _items[index];
            return _memory[range];
        }
    }

    private int _written;
    private IMemoryOwner<T> _owner;
    private Memory<T> _memory;

    private readonly MemoryPool<T> _memoryPool;
    private readonly List<Range> _items = [];

    public WritableBuffer(MemoryPool<T> allocator)
    {
        _memoryPool = allocator;
        _owner = HeapMemoryOwner<T>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(ReadOnlySpan<T> value)
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));

        if ((_memory.Length - _written) < value.Length)
        {
            _memory = _memoryPool.EnsureCapacity(
                ref _owner,
                Math.Max(value.Length + _written, 256),
                copyOnResize: true
            );
        }

        int start = _written;

        value.CopyTo(_memory.Span.Slice(start));
        _written += value.Length;
        _items.Add(new Range(start, start + value.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));
        _items.Clear();
        _written = 0;
    }

    public void Dispose()
    {
        _owner?.Dispose();
        this = default;
    }

    /// <summary>
    /// Copies the buffer to a new array and returns individual fields as <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public readonly ArraySegment<T>[] Preserve()
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));

        if (_items.Count == 0)
            return [];

        var array = new ArraySegment<T>[_items.Count];
        T[] buffer = GC.AllocateUninitializedArray<T>(length: _items[^1].End.GetOffset(_memory.Length));

        _memory.Slice(0, buffer.Length).CopyTo(buffer);

        for (int i = 0; i < _items.Count; i++)
        {
            int offset = _items[i].Start.GetOffset(_memory.Length);
            int count = _items[i].End.GetOffset(_memory.Length) - offset;

            array[i] = new ArraySegment<T>(buffer, offset, count);
        }

        return array;
    }
}
