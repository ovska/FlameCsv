using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Utilities;

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0064:Make readonly fields writable")]
internal struct WritableBuffer<T> : IDisposable where T : unmanaged, IBinaryInteger<T>
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
            return _memory[_items[index]];
        }
    }

    public readonly BufferFieldReader<T> CreateReader(CsvOptions<T> options, ReadOnlyMemory<T> record)
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));
        return new(options, record, _memory.Span, _items.AsSpan());
    }

    private int _index;
    private IMemoryOwner<T> _owner;
    private Memory<T> _memory;

    private readonly MemoryPool<T> _allocator;
    private readonly List<Range> _items = [];

    public WritableBuffer(MemoryPool<T> allocator)
    {
        _allocator = allocator;
        _owner = HeapMemoryOwner<T>.Empty;
    }

    public void Push(ReadOnlySpan<T> value)
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));

        if ((_memory.Length - _index) < value.Length)
        {
            _memory = _allocator.EnsureCapacity(ref _owner, Math.Max(value.Length + _index, 256), copyOnResize: true);
        }

        int start = _index;

        value.CopyTo(_memory.Span.Slice(start));
        _index += value.Length;
        _items.Add(new Range(start, start + value.Length));
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));
        _items.Clear();
        _index = 0;
    }

    public void Dispose()
    {
        _owner?.Dispose();
        this = default;
    }

    public readonly ReadOnlyMemory<T>[] Preserve()
    {
        ObjectDisposedException.ThrowIf(_items is null, typeof(WritableBuffer<T>));

        if (_items.Count == 0)
            Throw.InvalidOperation("Cannot preserve empty buffer");

        var array = new ReadOnlyMemory<T>[_items.Count];
        var buffer = new T[_items[^1].End.GetOffset(_memory.Length)];

        _memory.Slice(0, buffer.Length).CopyTo(buffer);

        for (int i = 0; i < _items.Count; i++)
        {
            array[i] = buffer.AsMemory(_items[i]);
        }

        return array;
    }
}
