using System.Diagnostics;

namespace FlameCsv.SourceGen.Helpers;

internal static class PooledList<T>
{
    private static readonly ObjectPool<List<T>> _pool = new(() => []);

    public static List<T> Acquire()
    {
        var instance = _pool.Allocate();
        Debug.Assert(instance.Count == 0);
        return instance;
    }

    public static void Release(List<T>? list)
    {
        // If the list is too big, don't return it to the pool.
        if (list is not { Count: < 256 }) return;
        list.Clear();
        _pool.Free(list);
    }
}
