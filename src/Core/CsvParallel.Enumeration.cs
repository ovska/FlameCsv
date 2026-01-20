using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Channels;
using FlameCsv.ParallelUtils;

namespace FlameCsv;

internal static partial class CsvParallel
{
    internal sealed class ParallelEnumerable<T>(
        Action<IConsumer<SlimList<T>>, CancellationToken> runParallel,
        CancellationToken userToken
    ) : IEnumerable<ArraySegment<T>>
    {
        public IEnumerator<ArraySegment<T>> GetEnumerator() => new ParallelEnumerator<T>(runParallel, userToken);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ParallelEnumerator<T> : IEnumerator<ArraySegment<T>>
    {
        private readonly BlockingCollection<SlimList<T>> _blockingCollection;
        private SlimList<T>? _current;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;
        private bool _done;

        public ParallelEnumerator(
            Action<IConsumer<SlimList<T>>, CancellationToken> parallelForEach,
            CancellationToken userToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();

            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(userToken, _enumeratorCts.Token);
            _pipelineToken = _pipelineCts.Token;

            // the managers don't need to be disposed on exception as the pooling is only valid for the lifetime of the operation
            _blockingCollection = [];

            EnumerationConsumer<T> consumer = new(_blockingCollection, null!, _pipelineToken);

            _parallelTask = Task.Run(
                () =>
                {
                    try
                    {
                        parallelForEach(consumer, _pipelineToken);
                    }
                    finally
                    {
                        Volatile.Write(ref _done, true);
                        _blockingCollection.CompleteAdding();
                    }
                },
                CancellationToken.None
            );
        }

        public ArraySegment<T> Current => _current!.AsArraySegment();
        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            return _blockingCollection.TryTake(out _current, Timeout.Infinite, _pipelineToken);
        }

        public void Dispose()
        {
            // If we haven't observed a terminal state yet, assume
            // the consumer is quitting early -> signal our side.
            if (!Volatile.Read(ref _done) && !_enumeratorCts.IsCancellationRequested)
            {
                _enumeratorCts.Cancel();
            }

            try
            {
                _parallelTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (_enumeratorCts.IsCancellationRequested)
            {
                // Enumerator early-exit -> swallow
            }
            finally
            {
                // no need to dispose _current here; the pooling is only valid for the lifetime of the operation
                _blockingCollection.Dispose();
                _pipelineCts.Dispose();
                _enumeratorCts.Dispose();
            }
        }

        public void Reset() => throw new NotSupportedException();
    }

    internal sealed class ParallelAsyncEnumerable<T>(
        Func<IConsumer<SlimList<T>>, CancellationToken, Task> runParallel,
        CancellationToken userToken
    ) : IAsyncEnumerable<ArraySegment<T>>
    {
        public IAsyncEnumerator<ArraySegment<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ParallelAsyncEnumerator<T>(runParallel, userToken, cancellationToken);
        }
    }

    private sealed class ParallelAsyncEnumerator<T> : IAsyncEnumerator<ArraySegment<T>>
    {
        private readonly Channel<SlimList<T>> _channel;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;

        public ArraySegment<T> Current => _current!.AsArraySegment();

        private SlimList<T>? _current;

        public ParallelAsyncEnumerator(
            Func<IConsumer<SlimList<T>>, CancellationToken, Task> parallelForEachAsync,
            CancellationToken userToken,
            CancellationToken getEnumeratorToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();
            _pipelineCts = CancellationTokenSource.CreateLinked(userToken, getEnumeratorToken, _enumeratorCts.Token);
            _pipelineToken = _pipelineCts.Token;

            _channel = Channel.CreateUnbounded<SlimList<T>>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                }
            );

            EnumerationConsumer<T> consumer = new(null!, _channel.Writer, _pipelineToken);

            _parallelTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await parallelForEachAsync(consumer, _pipelineToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _channel.Writer.Complete();
                    }
                },
                CancellationToken.None
            );
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (_channel.Reader.TryRead(out _current))
            {
                return ValueTask.FromResult(true);
            }

            return MoveNextWithAwait();
        }

        private async ValueTask<bool> MoveNextWithAwait()
        {
            while (await _channel.Reader.WaitToReadAsync(_pipelineToken).ConfigureAwait(false))
            {
                if (_channel.Reader.TryRead(out _current))
                {
                    return true;
                }
            }

            _current = null;
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            // If we haven't observed a terminal state yet, assume
            // the consumer is quitting early -> signal our side.
            _enumeratorCts.Cancel();

            try
            {
                await _parallelTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_enumeratorCts.IsCancellationRequested)
            {
                // Enumerator early-exit -> swallow
            }
            finally
            {
                _pipelineCts.Dispose();
                _enumeratorCts.Dispose();
            }
        }
    }
}

file sealed class EnumerationConsumer<T>(
    BlockingCollection<SlimList<T>> collection,
    ChannelWriter<SlimList<T>> channel,
    CancellationToken cancellationToken
) : IConsumer<SlimList<T>>
{
    public void Consume(SlimList<T> state, Exception? ex)
    {
        if (ex is null)
        {
            collection.Add(state, cancellationToken);
        }
    }

    public ValueTask ConsumeAsync(SlimList<T> state, Exception? ex, CancellationToken cancellationToken)
    {
        return ex is null ? channel.WriteAsync(state, cancellationToken)
            : cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken)
            : ValueTask.CompletedTask;
    }
}

file static class Extensions
{
    extension(CancellationTokenSource)
    {
        public static CancellationTokenSource CreateLinked(
            CancellationToken first,
            CancellationToken second,
            CancellationToken third
        )
        {
            if (!first.CanBeCanceled)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(second, third);
            }

            if (!second.CanBeCanceled)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(first, third);
            }

            Check.True(third.CanBeCanceled, "The third token must be cancellable.");
            return CancellationTokenSource.CreateLinkedTokenSource(first, second, third);
        }
    }
}
