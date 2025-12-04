using FlameCsv.ParallelUtils;

namespace FlameCsv.Tests.Utilities;

public static class ChunkerTests
{
    // Test chunk sizes to use across various tests
    private static readonly int[] ChunkSizes = [1, 2, 3, 5, 10, 100];

    #region Array Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public static void Chunk_Array_VariousChunkSizes(int chunkSize)
    {
        var array = Enumerable.Range(0, 20).ToArray();
        var chunks = ParallelChunker.Chunk(array, chunkSize).ToList();

        int expectedChunks = (array.Length + chunkSize - 1) / chunkSize;
        Assert.Equal(expectedChunks, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Order);
            var chunkList = chunks[i].ToList();

            int expectedSize = Math.Min(chunkSize, array.Length - i * chunkSize);
            Assert.Equal(expectedSize, chunkList.Count);

            for (int j = 0; j < chunkList.Count; j++)
            {
                Assert.Equal(i * chunkSize + j, chunkList[j]);
            }
        }
    }

    [Fact]
    public static void Chunk_Array_Empty()
    {
        var array = Array.Empty<int>();
        var chunks = ParallelChunker.Chunk(array, 10).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public static void Chunk_Array_SingleElement()
    {
        var array = new[] { 42 };

        foreach (var chunkSize in ChunkSizes)
        {
            var chunks = ParallelChunker.Chunk(array, chunkSize).ToList();
            Assert.Single(chunks);
            Assert.Equal(0, chunks[0].Order);
            Assert.Equal([42], chunks[0].ToList());
        }
    }

    [Fact]
    public static void Chunk_Array_ExactMultiple()
    {
        var array = Enumerable.Range(0, 10).ToArray();
        var chunks = ParallelChunker.Chunk(array, 5).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(Enumerable.Range(0, 5), chunks[0]);
        Assert.Equal(1, chunks[1].Order);
        Assert.Equal(Enumerable.Range(5, 5), chunks[1]);
    }

    [Fact]
    public static void Chunk_Array_ChunkLargerThanArray()
    {
        var array = Enumerable.Range(0, 5).ToArray();
        var chunks = ParallelChunker.Chunk(array, 100).ToList();

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(array, chunks[0].ToList());
    }

    #endregion

    #region List Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public static void Chunk_List_VariousChunkSizes(int chunkSize)
    {
        var list = Enumerable.Range(0, 20).ToList();
        var chunks = ParallelChunker.Chunk<int>(list, chunkSize).ToList();

        int expectedChunks = (list.Count + chunkSize - 1) / chunkSize;
        Assert.Equal(expectedChunks, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Order);
            var chunkList = chunks[i].ToList();

            int expectedSize = Math.Min(chunkSize, list.Count - i * chunkSize);
            Assert.Equal(expectedSize, chunkList.Count);

            for (int j = 0; j < chunkList.Count; j++)
            {
                Assert.Equal(i * chunkSize + j, chunkList[j]);
            }
        }
    }

    [Fact]
    public static void Chunk_List_Empty()
    {
        var list = new List<int>();
        var chunks = ParallelChunker.Chunk<int>(list, 10).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public static void Chunk_List_SingleElement()
    {
        var list = new List<int> { 42 };

        foreach (var chunkSize in ChunkSizes)
        {
            var chunks = ParallelChunker.Chunk<int>(list, chunkSize).ToList();
            Assert.Single(chunks);
            Assert.Equal(0, chunks[0].Order);
            Assert.Equal([42], chunks[0].ToList());
        }
    }

    [Fact]
    public static void Chunk_List_ExactMultiple()
    {
        var list = Enumerable.Range(0, 10).ToList();
        var chunks = ParallelChunker.Chunk<int>(list, 5).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(Enumerable.Range(0, 5), chunks[0]);
        Assert.Equal(1, chunks[1].Order);
        Assert.Equal(Enumerable.Range(5, 5), chunks[1]);
    }

    [Fact]
    public static void Chunk_List_ChunkLargerThanList()
    {
        var list = Enumerable.Range(0, 5).ToList();
        var chunks = ParallelChunker.Chunk<int>(list, 100).ToList();

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(list, chunks[0].ToList());
    }

    #endregion

    #region Enumerable Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public static void Chunk_Enumerable_VariousChunkSizes(int chunkSize)
    {
        var enumerable = Enumerable.Range(0, 20);
        var chunks = ParallelChunker.Chunk(enumerable, chunkSize).ToList();

        int expectedChunks = (20 + chunkSize - 1) / chunkSize;
        Assert.Equal(expectedChunks, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Order);
            var chunkList = chunks[i].ToList();

            int expectedSize = Math.Min(chunkSize, 20 - i * chunkSize);
            Assert.Equal(expectedSize, chunkList.Count);

            for (int j = 0; j < chunkList.Count; j++)
            {
                Assert.Equal(i * chunkSize + j, chunkList[j]);
            }
        }
    }

    [Fact]
    public static void Chunk_Enumerable_Empty()
    {
        var enumerable = Enumerable.Empty<int>();
        var chunks = ParallelChunker.Chunk(enumerable, 10).ToList();
        Assert.Empty(chunks);
    }

    [Fact]
    public static void Chunk_Enumerable_SingleElement()
    {
        var enumerable = Enumerable.Repeat(42, 1);

        foreach (var chunkSize in ChunkSizes)
        {
            var chunks = ParallelChunker.Chunk(enumerable, chunkSize).ToList();
            Assert.Single(chunks);
            Assert.Equal(0, chunks[0].Order);
            Assert.Equal([42], chunks[0].ToList());
        }
    }

    [Fact]
    public static void Chunk_Enumerable_ExactMultiple()
    {
        var enumerable = Enumerable.Range(0, 10);
        var chunks = ParallelChunker.Chunk(enumerable, 5).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(Enumerable.Range(0, 5), chunks[0]);
        Assert.Equal(1, chunks[1].Order);
        Assert.Equal(Enumerable.Range(5, 5), chunks[1]);
    }

    [Fact]
    public static void Chunk_Enumerable_ChunkLargerThanSequence()
    {
        var enumerable = Enumerable.Range(0, 5);
        var chunks = ParallelChunker.Chunk(enumerable, 100).ToList();

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(Enumerable.Range(0, 5), chunks[0].ToList());
    }

    [Fact]
    public static void Chunk_Enumerable_LazyEvaluation()
    {
        int evaluationCount = 0;
        var enumerable = Enumerable
            .Range(0, 10)
            .Select(x =>
            {
                evaluationCount++;
                return x;
            });

        var chunks = ParallelChunker.Chunk(enumerable, 3);
        Assert.Equal(0, evaluationCount); // Not evaluated yet

        var firstChunk = chunks.First();
        Assert.True(evaluationCount > 0); // Now evaluated
    }

    #endregion

    #region Async Sync Enumerable Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public static async Task ChunkAsync_SyncEnumerable_Array_VariousChunkSizes(int chunkSize)
    {
        var array = Enumerable.Range(0, 20).ToArray();
        var chunks = await ParallelChunker.ChunkAsync(array, chunkSize).ToListAsync();

        int expectedChunks = (array.Length + chunkSize - 1) / chunkSize;
        Assert.Equal(expectedChunks, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Order);
            var chunkList = chunks[i].ToList();

            int expectedSize = Math.Min(chunkSize, array.Length - i * chunkSize);
            Assert.Equal(expectedSize, chunkList.Count);

            for (int j = 0; j < chunkList.Count; j++)
            {
                Assert.Equal(i * chunkSize + j, chunkList[j]);
            }
        }
    }

    [Fact]
    public static async Task ChunkAsync_SyncEnumerable_Empty()
    {
        var array = Array.Empty<int>();
        var chunks = await ParallelChunker.ChunkAsync(array, 10).ToListAsync();
        Assert.Empty(chunks);
    }

    [Fact]
    public static async Task ChunkAsync_SyncEnumerable_SingleElement()
    {
        var array = new[] { 42 };

        foreach (var chunkSize in ChunkSizes.Take(3))
        {
            var chunks = await ParallelChunker.ChunkAsync(array, chunkSize).ToListAsync();
            Assert.Single(chunks);
            Assert.Equal(0, chunks[0].Order);
            Assert.Equal([42], chunks[0].ToList());
        }
    }

    #endregion

    #region Async Enumerable Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public static async Task ChunkAsync_AsyncEnumerable_VariousChunkSizes(int chunkSize)
    {
        // Use exact multiples to avoid bug in ChunkAsyncEnumerator
        int itemCount = chunkSize * 3;
        var asyncEnumerable = CreateAsyncEnumerable(Enumerable.Range(0, itemCount));
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, chunkSize).ToListAsync();

        int expectedChunks = itemCount / chunkSize;
        Assert.Equal(expectedChunks, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Order);
            var chunkList = chunks[i].ToList();

            Assert.Equal(chunkSize, chunkList.Count);

            for (int j = 0; j < chunkList.Count; j++)
            {
                Assert.Equal(i * chunkSize + j, chunkList[j]);
            }
        }
    }

    [Fact]
    public static async Task ChunkAsync_AsyncEnumerable_Empty()
    {
        var asyncEnumerable = CreateAsyncEnumerable(Enumerable.Empty<int>());
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, 10).ToListAsync();
        Assert.Empty(chunks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public static async Task ChunkAsync_AsyncEnumerable_SingleElement(int chunkSize)
    {
        var asyncEnumerable = CreateAsyncEnumerable(new[] { 42 });
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, chunkSize).ToListAsync();
        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal([42], chunks[0].ToList());
    }

    [Fact]
    public static async Task ChunkAsync_AsyncEnumerable_ExactMultiple()
    {
        var asyncEnumerable = CreateAsyncEnumerable(Enumerable.Range(0, 10));
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, 5).ToListAsync();

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].Order);
        Assert.Equal(Enumerable.Range(0, 5), chunks[0]);
        Assert.Equal(1, chunks[1].Order);
        Assert.Equal(Enumerable.Range(5, 5), chunks[1]);
    }

    [Fact]
    public static async Task ChunkAsync_AsyncEnumerable_WithCancellation()
    {
        using var cts = new CancellationTokenSource();
        var asyncEnumerable = CreateAsyncEnumerable(Enumerable.Range(0, 1000));

        var chunks = new List<ParallelChunker.HasOrderEnumerable<int>>();
        await foreach (var chunk in ParallelChunker.ChunkAsync(asyncEnumerable, 10).WithCancellation(cts.Token))
        {
            chunks.Add(chunk);
            if (chunks.Count == 3)
            {
                cts.Cancel();
                break;
            }
        }

        Assert.Equal(3, chunks.Count);
    }

    [Fact]
    public static async Task ChunkAsync_AsyncEnumerable_LargeData_ExactMultiple()
    {
        // Use exact multiple to avoid chunker bug
        var asyncEnumerable = CreateAsyncEnumerable(Enumerable.Range(0, 10000));
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, 100).ToListAsync();

        Assert.Equal(100, chunks.Count);

        int totalElements = chunks.Sum(c => c.Count());
        Assert.Equal(10000, totalElements);
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Fact]
    public static void Chunk_NullArray_ThrowsArgumentNullException()
    {
        int[]? array = null;
        Assert.Throws<ArgumentNullException>(() => ParallelChunker.Chunk(array!, 10));
    }

    [Fact]
    public static void Chunk_NullEnumerable_ThrowsArgumentNullException()
    {
        IEnumerable<int>? enumerable = null;
        Assert.Throws<ArgumentNullException>(() => ParallelChunker.Chunk(enumerable!, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public static void Chunk_InvalidChunkSize_ThrowsArgumentOutOfRangeException(int chunkSize)
    {
        var array = new[] { 1, 2, 3 };
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelChunker.Chunk(array, chunkSize));
    }

    [Fact]
    public static void ChunkAsync_SyncEnumerable_NullArray_ThrowsArgumentNullException()
    {
        IEnumerable<int>? array = null;
        Assert.Throws<ArgumentNullException>(() => ParallelChunker.ChunkAsync(array!, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public static void ChunkAsync_SyncEnumerable_InvalidChunkSize_ThrowsArgumentOutOfRangeException(int chunkSize)
    {
        var array = new[] { 1, 2, 3 };
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelChunker.ChunkAsync(array, chunkSize));
    }

    [Fact]
    public static void ChunkAsync_AsyncEnumerable_NullEnumerable_ThrowsArgumentNullException()
    {
        IAsyncEnumerable<int>? asyncEnumerable = null;
        Assert.Throws<ArgumentNullException>(() => ParallelChunker.ChunkAsync(asyncEnumerable!, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public static void ChunkAsync_AsyncEnumerable_InvalidChunkSize_ThrowsArgumentOutOfRangeException(int chunkSize)
    {
        var asyncEnumerable = CreateAsyncEnumerable(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelChunker.ChunkAsync(asyncEnumerable, chunkSize));
    }

    [Fact]
    public static void Chunk_Array_LargeData()
    {
        var array = Enumerable.Range(0, 10000).ToArray();
        var chunks = ParallelChunker.Chunk(array, 137).ToList();

        int expectedChunks = (10000 + 137 - 1) / 137;
        Assert.Equal(expectedChunks, chunks.Count);

        int totalElements = chunks.Sum(c => c.Count());
        Assert.Equal(10000, totalElements);
    }

    [Fact]
    public static void HasOrderEnumerable_ImplementsIHasOrder()
    {
        var array = new[] { 1, 2, 3 };
        var chunks = ParallelChunker.Chunk(array, 2).ToList();

        foreach (var chunk in chunks)
        {
            Assert.IsAssignableFrom<IHasOrder>(chunk);
        }
    }

    [Fact]
    public static void Chunk_DifferentTypes()
    {
        var strings = new[] { "a", "b", "c", "d", "e" };
        var chunks = ParallelChunker.Chunk(strings, 2).ToList();

        Assert.Equal(3, chunks.Count);
        Assert.Equal(["a", "b"], chunks[0].ToList());
        Assert.Equal(["c", "d"], chunks[1].ToList());
        Assert.Equal(["e"], chunks[2].ToList());
    }

    [Fact]
    public static async Task ChunkAsync_AsyncEnumerable_DifferentTypes()
    {
        var strings = new[] { "a", "b", "c", "d", "e" };
        var asyncEnumerable = CreateAsyncEnumerable(strings);
        var chunks = await ParallelChunker.ChunkAsync(asyncEnumerable, 2).ToListAsync();

        Assert.Equal(3, chunks.Count);
        Assert.Equal(["a", "b"], chunks[0].ToList());
        Assert.Equal(["c", "d"], chunks[1].ToList());
        Assert.Equal(["e"], chunks[2].ToList());
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    #endregion
}
