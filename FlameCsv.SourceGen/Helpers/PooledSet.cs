using System.Diagnostics;

namespace FlameCsv.SourceGen.Helpers;

internal static class PooledSet<T>
{
    private static readonly ObjectPool<HashSet<T>> _pool = new(() => []);

    public static HashSet<T> Acquire()
    {
        var instance = _pool.Allocate();
        Debug.Assert(instance.Count == 0);
        return instance;
    }

    public static void Release(HashSet<T>? set)
    {
        if (set is null) return;

        if (set.Count >= 256)
        {
            // don't pool large sets
            return;
        }

        set.Clear();
        _pool.Free(set);
    }
}
