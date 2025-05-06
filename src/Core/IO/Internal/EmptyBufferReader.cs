namespace FlameCsv.IO.Internal;

internal sealed class EmptyBufferReader<T> : ICsvBufferReader<T>
    where T : unmanaged
{
    public static EmptyBufferReader<T> Instance { get; } = new();

    private EmptyBufferReader() { }

    public void Dispose() { }

    public ValueTask DisposeAsync() => default;

    public CsvReadResult<T> Read() => CsvReadResult<T>.Completed;

    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken)
            : new ValueTask<CsvReadResult<T>>(CsvReadResult<T>.Completed);
    }

    public void Advance(int count) => throw new NotSupportedException();

    public bool TryReset() => true;
}
