namespace FlameCsv.IO.Internal;

internal interface IParallelReader<T> : IEnumerable<Chunk<T>>, IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>;
