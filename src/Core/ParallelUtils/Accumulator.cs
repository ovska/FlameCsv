using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.ParallelUtils;

internal sealed class Accumulator<T>(int chunkSize) : IConsumable
{
    private readonly T[] _array = new T[chunkSize];

    public bool IsEmpty => _index == 0;
    public bool ShouldConsume => _index >= _array.Length;

    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _array.AsSpan(0, _index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<T> AsArraySegment() => new(_array, 0, _index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        Debug.Assert(_index < _array.Length);
        _array[_index++] = item;
    }
}
