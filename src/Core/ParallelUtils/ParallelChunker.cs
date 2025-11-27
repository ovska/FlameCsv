using System.Collections;
using FlameCsv.Utilities;

namespace FlameCsv.ParallelUtils;

internal static class ParallelChunker
{
    public sealed class HasOrderEnumerable<T>(int order, IEnumerable<T> inner) : IHasOrder, IEnumerable<T>
    {
        public int Order => order;

        public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static IEnumerable<HasOrderEnumerable<T>> Chunk<T>(IEnumerable<T> items, int chunkSize)
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

    public static IAsyncEnumerable<HasOrderEnumerable<T>> ChunkAsync<T>(IEnumerable<T> items, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        return new SyncToAsyncEnumerable<HasOrderEnumerable<T>>(Chunk(items, chunkSize));
    }

    public static IAsyncEnumerable<HasOrderEnumerable<T>> ChunkAsync<T>(IAsyncEnumerable<T> items, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        if (items is IEnumerable<T> syncItems)
        {
            return new SyncToAsyncEnumerable<HasOrderEnumerable<T>>(Chunk(syncItems, chunkSize));
        }

        return Core();

        async IAsyncEnumerable<HasOrderEnumerable<T>> Core()
        {
            int order = 0;
            List<T> buffer = new(chunkSize);

            await foreach (var item in items.ConfigureAwait(false))
            {
                buffer.Add(item);

                if (buffer.Count >= chunkSize)
                {
                    yield return new HasOrderEnumerable<T>(order++, buffer);
                    buffer = new(chunkSize);
                }
            }

            if (buffer.Count > 0)
            {
                yield return new HasOrderEnumerable<T>(order++, buffer);
            }
        }
    }

    private static IEnumerable<HasOrderEnumerable<T>> ChunkArray<T>(T[] array, int chunkSize)
    {
        int order = 0;

        for (int i = 0; i < array.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, array.Length - i);
            yield return new(order++, new ArraySegment<T>(array, i, length));
        }
    }

    private static IEnumerable<HasOrderEnumerable<T>> ChunkList<T>(IList<T> list, int chunkSize)
    {
        int order = 0;

        for (int i = 0; i < list.Count; i += chunkSize)
        {
            int length = Math.Min(chunkSize, list.Count - i);
            yield return new(order++, new ListSegment<T>(list, i, length));
        }
    }

    private static IEnumerable<HasOrderEnumerable<T>> ChunkEnumerable<T>(IEnumerable<T> items, int chunkSize)
    {
        int order = 0;

        foreach (var chunk in items.Chunk(chunkSize))
        {
            yield return new(order++, chunk);
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
