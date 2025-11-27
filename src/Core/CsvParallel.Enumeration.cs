using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Channels;
using FlameCsv.ParallelUtils;

namespace FlameCsv;

internal static partial class CsvParallel
{
    internal sealed class ParallelEnumerable<T>(
        Action<Consume<Accumulator<T>>, CancellationToken> runParallel,
        CancellationToken userToken
    ) : IEnumerable<ArraySegment<T>>
    {
        public IEnumerator<ArraySegment<T>> GetEnumerator() => new ParallelEnumerator<T>(runParallel, userToken);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ParallelEnumerator<T> : IEnumerator<ArraySegment<T>>
    {
        private readonly BlockingCollection<Accumulator<T>> _blockingCollection;
        private Accumulator<T>? _current;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;
        private bool _done;

        public ParallelEnumerator(
            Action<Consume<Accumulator<T>>, CancellationToken> parallelForEach,
            CancellationToken userToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();

            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(userToken, _enumeratorCts.Token);
            _pipelineToken = _pipelineCts.Token;

            // the managers don't need to be disposed on exception as the pooling is only valid for the lifetime of the operation
            _blockingCollection = [];

            _parallelTask = Task.Run(
                () =>
                {
                    try
                    {
                        parallelForEach(
                            (in list, ex) =>
                            {
                                if (ex is null)
                                {
                                    _blockingCollection.Add(list, _pipelineToken);
                                }
                            },
                            _pipelineToken
                        );
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
        Func<ConsumeAsync<Accumulator<T>>, CancellationToken, Task> runParallel,
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
        private readonly Channel<Accumulator<T>> _channel;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;

        public ArraySegment<T> Current => _current!.AsArraySegment();

        private Accumulator<T>? _current;

        public ParallelAsyncEnumerator(
            Func<ConsumeAsync<Accumulator<T>>, CancellationToken, Task> parallelForEachAsync,
            CancellationToken userToken,
            CancellationToken getEnumeratorToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();
            _pipelineCts = CancellationTokenSource.CreateLinked(userToken, getEnumeratorToken, _enumeratorCts.Token);
            _pipelineToken = _pipelineCts.Token;

            _channel = Channel.CreateUnbounded<Accumulator<T>>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                }
            );

            _parallelTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await parallelForEachAsync(
                                (list, ex, ct) =>
                                    ex is null ? _channel.Writer.WriteAsync(list, ct)
                                    : ct.IsCancellationRequested ? ValueTask.FromCanceled(ct)
                                    : ValueTask.CompletedTask,
                                _pipelineToken
                            )
                            .ConfigureAwait(false);
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
                // no need to dispose _current here; the pooling is only valid for the lifetime of the operation
                _pipelineCts.Dispose();
                _enumeratorCts.Dispose();
            }
        }
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

            return CancellationTokenSource.CreateLinkedTokenSource(first, second, third);
        }
    }
}
