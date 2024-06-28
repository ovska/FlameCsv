using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FlameCsv.Tests.Utilities;

internal sealed class ReturnTrackingArrayPool<T> : ArrayPool<T>, IDisposable
{
    public bool TrackStackTraces { get; set; }

    private readonly ConcurrentDictionary<T[], StackTrace?> _values = [];
    private int rentedCount;
    private int returnedCount;

    public void Dispose()
    {
        if (!_values.IsEmpty)
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

        Interlocked.Increment(ref rentedCount);
        var arr = ArrayPool<T>.Shared.Rent(minimumLength);
        _values.TryAdd(arr, TrackStackTraces ? new StackTrace(fNeedFileInfo: true) : null);
        return arr;
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        if (array.Length > 0 && !_values.TryRemove(array, out _))
        {
            throw new InvalidOperationException("The returned array was not rented from the pool.");
        }

        if (array.Length != 0)
        {
            Interlocked.Increment(ref returnedCount);
            ArrayPool<T>.Shared.Return(array);
        }
    }
}
