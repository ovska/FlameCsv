using System.Diagnostics;

namespace FlameCsv.SourceGen.Helpers;

internal static class PooledStringBuilder
{
    private static readonly ObjectPool<StringBuilder> _pool = new(() => new StringBuilder(capacity: 256));

    public static StringBuilder Acquire()
    {
        var instance = _pool.Allocate();
        Debug.Assert(instance.Length == 0);
        return instance;
    }

    public static void Release(StringBuilder? sb)
    {
        if (sb is null) return;

        if (sb.Capacity > 256)
        {
            // don't pool fragmented builders
            return;
        }

        sb.Clear();
        _pool.Free(sb);
    }
}
