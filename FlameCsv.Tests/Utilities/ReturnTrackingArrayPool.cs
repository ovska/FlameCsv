using System.Buffers;
using System.Diagnostics;
using System.Numerics;

namespace FlameCsv.Tests.Utilities;

internal sealed class ReturnTrackingArrayPool<T> : ArrayPool<T>, IDisposable
{
    public bool TrackStackTraces { get; set; }

    private readonly Dictionary<T[], StackTrace?> _values = [];
    private int rentedCount;
    private int returnedCount;

    public void Dispose()
    {
        if (_values.Count > 0)
        {
            throw new InvalidOperationException(
                $"{_values.Count} arrays not returned to pool, {returnedCount} out of {rentedCount}. " +
                Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine, _values.Select(kvp => kvp.Value)));
        }
    }

    public override T[] Rent(int minimumLength)
    {
        if (minimumLength == 0)
        {
            return [];
        }

        rentedCount++;

        var arr = new T[BitOperations.RoundUpToPowerOf2((uint)minimumLength)];
        _values.Add(arr, TrackStackTraces ? new StackTrace(fNeedFileInfo: true) : null);
        return arr;
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        if (array.Length > 0 && !_values.Remove(array))
        {
            throw new InvalidOperationException("The returned array was not rented from the pool.");
        }

        returnedCount++;
    }
}
