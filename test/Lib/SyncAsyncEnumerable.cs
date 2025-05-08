using JetBrains.Annotations;

namespace FlameCsv.Tests;

public static class SyncAsyncEnumerable
{
    public static SyncAsyncEnumerable<T> Create<T>(params T[] inner) => new(inner);
    public static SyncAsyncEnumerable<T> Create<T>(IEnumerable<T> inner) => new(inner);
}

public sealed class SyncAsyncEnumerable<T>(IEnumerable<T> inner) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled) cancellationToken = TestContext.Current.CancellationToken;
        return new Enumerator(inner.GetEnumerator(), cancellationToken);
    }

    public sealed class Enumerator(
        [HandlesResourceDisposal] IEnumerator<T> inner,
        CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(cancellationToken);

            return new(inner.MoveNext());
        }
    }
}
