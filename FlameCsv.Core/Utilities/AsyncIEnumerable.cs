namespace FlameCsv.Utilities;

internal sealed class AsyncIEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IEnumerable<T> _source;

    public AsyncIEnumerable(IEnumerable<T> source)
    {
        _source = source;
    }

    public AsyncIEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new(_source.GetEnumerator(), cancellationToken);
    }

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }

    internal sealed class AsyncIEnumerator : IAsyncEnumerator<T>
    {
        public T Current => _source.Current;

        private readonly IEnumerator<T> _source;
        private readonly CancellationToken _cancellationToken;

        public AsyncIEnumerator(IEnumerator<T> source, CancellationToken cancellationToken)
        {
            _source = source;
            _cancellationToken = cancellationToken;
        }

        public ValueTask DisposeAsync()
        {
            _source.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (!_cancellationToken.IsCancellationRequested)
                return new(_source.MoveNext());

            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }
    }
}
