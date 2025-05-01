using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal struct ListBuilder<T> : IDisposable
    where T : struct
{
    private T[] _array;
    private int _count;

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ListBuilder(int capacity = 512)
    {
        _array = ArrayPool<T>.Shared.Rent(capacity);
        _count = 0;
    }

    /// <summary>
    /// Allocates <paramref name="count"/> elements in the array and returns a span to write to.
    /// </summary>
    public Span<T> ResetAndGetCapacitySpan(int count)
    {
        _count = count;

        if ((uint)_count > (uint)_array.Length)
        {
            int newSize = Math.Max(_array.Length * 2, _count);
            ArrayPool<T>.Shared.Resize(ref _array, newSize);
        }

        return new Span<T>(_array, 0, _count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref _array[0], _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T[] UnsafeGetArray() => _array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T UnsafeGetRef(out int count)
    {
        count = _count;
        return ref MemoryMarshal.GetArrayDataReference(_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        if ((uint)_count >= (uint)_array.Length)
        {
            PushWithResize(item);
        }
        else
        {
            _array[_count++] = item;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PushWithResize(T item)
    {
        int newSize = Math.Max(_array.Length * 2, _count + 1);
        ArrayPool<T>.Shared.Resize(ref _array, newSize);
        _array[_count++] = item;
    }

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array);
        _array = [];
    }
}
