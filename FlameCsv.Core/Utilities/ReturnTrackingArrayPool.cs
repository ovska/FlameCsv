using System.Buffers;
using System.Numerics;

namespace FlameCsv.Utilities;

internal sealed class ReturnTrackingArrayPool<T> : ArrayPool<T>, IDisposable
{
    private readonly HashSet<T[]> _values = new();
    private int rentedCount;
    private int returnedCount;

    public void Dispose()
    {
        if (_values.Count > 0)
        {
            throw new InvalidOperationException(
                $"{_values.Count} arrays not returned to pool, {returnedCount} out of {rentedCount}");
        }
    }

    public override T[] Rent(int minimumLength)
    {
        rentedCount++;

        if (minimumLength == 0)
        {
            return Array.Empty<T>();
        }

        var arr = new T[BitOperations.RoundUpToPowerOf2((uint)minimumLength)];
        _values.Add(arr);
        return arr;
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        returnedCount++;

        if (array.Length > 0 && !_values.Remove(array))
        {
            throw new InvalidOperationException("Array not rented from pool.");
        }
    }
}
