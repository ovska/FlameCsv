namespace FlameCsv.ParallelUtils;

internal static class ParallelChunker
{
    public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> items, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        return items switch
        {
            T[] array => ChunkArray(array, chunkSize),
            IList<T> list => ChunkList(list, chunkSize),
            _ => ChunkEnumerable(items, chunkSize),
        };
    }

    private static IEnumerable<IEnumerable<T>> ChunkArray<T>(T[] array, int chunkSize)
    {
        for (int i = 0; i < array.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, array.Length - i);
            yield return new ArraySegment<T>(array, i, length);
        }
    }

    private static IEnumerable<IEnumerable<T>> ChunkList<T>(IList<T> list, int chunkSize)
    {
        for (int i = 0; i < list.Count; i += chunkSize)
        {
            int length = Math.Min(chunkSize, list.Count - i);
            yield return new ListSegment<T>(list, i, length);
        }
    }

    private static IEnumerable<IEnumerable<T>> ChunkEnumerable<T>(IEnumerable<T> items, int chunkSize)
    {
        foreach (var chunk in items.Chunk(chunkSize))
        {
            yield return chunk;
        }
    }

    private sealed class ListSegment<T> : IEnumerable<T>
    {
        private readonly IList<T> _list;
        private readonly int _offset;
        private readonly int _count;

        public ListSegment(IList<T> list, int offset, int count)
        {
            _list = list;
            _offset = offset;
            _count = count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _list[_offset + i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
