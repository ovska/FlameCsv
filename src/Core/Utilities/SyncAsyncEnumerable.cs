namespace FlameCsv.Utilities;

internal sealed class SyncToAsyncEnumerable<T>(IEnumerable<T> inner) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(inner.GetEnumerator(), cancellationToken);
    }

    private sealed class Enumerator(IEnumerator<T> inner, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return cancellationToken.IsCancellationRequested
                ? ValueTask.FromCanceled<bool>(cancellationToken)
                : new ValueTask<bool>(inner.MoveNext());
        }
    }
}
