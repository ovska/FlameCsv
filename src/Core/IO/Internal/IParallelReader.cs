using System.Collections;

namespace FlameCsv.IO.Internal;

internal interface IParallelReader<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    Chunk<T>? Read();
    ValueTask<Chunk<T>?> ReadAsync(CancellationToken cancellationToken = default);

    IEnumerable<Chunk<T>> AsEnumerable() => new ParallelEnumerable<T>(this);
    IAsyncEnumerable<Chunk<T>> AsAsyncEnumerable() => new ParallelAsyncEnumerable<T>(this);
}

file sealed class ParallelEnumerable<T>(IParallelReader<T> reader) : IEnumerable<Chunk<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    public IEnumerator<Chunk<T>> GetEnumerator() => new Enumerator<T>(reader);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

file sealed class ParallelAsyncEnumerable<T>(IParallelReader<T> reader) : IAsyncEnumerable<Chunk<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    public IAsyncEnumerator<Chunk<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new AsyncEnumerator<T>(reader, cancellationToken);
}

file sealed class Enumerator<T>(IParallelReader<T> reader) : IEnumerator<Chunk<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private Chunk<T>? _current;

    public Chunk<T> Current => _current!;
    object IEnumerator.Current => Current;

    public void Dispose() => reader.Dispose();

    public bool MoveNext()
    {
        _current = reader.Read();
        return _current is not null;
    }

    public void Reset() => throw new NotSupportedException();
}

file sealed class AsyncEnumerator<T>(IParallelReader<T> reader, CancellationToken cancellationToken)
    : IAsyncEnumerator<Chunk<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private Chunk<T>? _current;

    public Chunk<T> Current => _current!;

    public ValueTask DisposeAsync() => reader.DisposeAsync();

    public async ValueTask<bool> MoveNextAsync()
    {
        _current = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return _current is not null;
    }
}
