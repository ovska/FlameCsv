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
        if (list is null) return;

        if (list.Count >= 256)
        {
            // don't pool large lists
            return;
        }

        list.Clear();
        _pool.Free(list);
    }
}
