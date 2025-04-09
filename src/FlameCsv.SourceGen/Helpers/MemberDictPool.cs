using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen.Helpers;

internal static class MemberDictPool
{
    private static readonly ObjectPool<SortedDictionary<int, IMemberModel?>> _pool = new(() => new());

    public static SortedDictionary<int, IMemberModel?> Acquire()
    {
        return _pool.Allocate();
    }

    public static void Release(SortedDictionary<int, IMemberModel?>? dictionary)
    {
        // If the dictionary is too big, don't return it to the pool.
        if (dictionary is not { Count: < 16 }) return;
        dictionary.Clear();
        _pool.Free(dictionary);
    }
}
