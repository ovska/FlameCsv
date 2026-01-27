using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FlameCsv.ParallelUtils;

namespace FlameCsv;

internal static partial class CsvParallel
{
    internal sealed class ParallelEnumerable<T>(
        Func<IConsumer<SlimList<T>>, CancellationToken, Task> runParallel,
        CsvParallelOptions parallelOptions
    ) : IEnumerable<ArraySegment<T>>
    {
        public IEnumerator<ArraySegment<T>> GetEnumerator() => new ParallelEnumerator<T>(runParallel, parallelOptions);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ParallelEnumerator<T> : IEnumerator<ArraySegment<T>>
    {
        private readonly ConcurrentQueue<SlimList<T>> _queue;
        private readonly SemaphoreSlim _semaphore;
        private SlimList<T>? _current;

        /// <summary>Token to cancel the background tasks if enumeration ends early (e.g. via <c>break</c>)</summary>
        private readonly CancellationTokenSource _enumeratorCts;

        /// <summary>Combined token for the entire pipeline (user + enumerator), early exit, user cancellation, or exception</summary>
        private readonly CancellationTokenSource _pipelineCts;

        /// <summary>Task running the parallel operation in the background</summary>
        private readonly Task _parallelTask;
        private bool _done;

        public ParallelEnumerator(
            Func<IConsumer<SlimList<T>>, CancellationToken, Task> parallelForEach,
            CsvParallelOptions parallelOptions
        )
        {
            _enumeratorCts = new CancellationTokenSource();

            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(
                parallelOptions.CancellationToken,
                _enumeratorCts.Token
            );

            _queue = new ConcurrentQueue<SlimList<T>>();
            _semaphore = new SemaphoreSlim(0);

            TaskCompletionSource parallelTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _parallelTask = parallelTcs.Task;

            Thread producerThread = new Thread(() =>
            {
                try
                {
                    parallelForEach(new Consumer(_queue, _semaphore, _pipelineCts.Token), _pipelineCts.Token)
                        .GetAwaiter()
                        .GetResult();
                    parallelTcs.TrySetResult();
                }
                catch (OperationCanceledException ex)
                {
                    parallelTcs.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    parallelTcs.TrySetException(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _done, true);
                    _semaphore.Release();
                }
            })
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal,
            };

            producerThread.Start();
        }

        public ArraySegment<T> Current => _current!.AsArraySegment();
        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_queue.TryDequeue(out _current))
                return true;

            while (true)
            {
                _semaphore.Wait(_pipelineCts.Token);

                if (_queue.TryDequeue(out _current))
                    return true;

                if (Volatile.Read(ref _done))
                {
                    _current = null;
                    return false;
                }
            }
        }

        public void Dispose()
        {
            // if _done is not already true, we are exiting early via "break" in a foreach or similar
            bool wasEarlyExit = !Interlocked.Exchange(ref _done, true);

            // If we haven't observed a terminal state yet, assume the consumer is quitting early -> signal our side.
            if (wasEarlyExit)
            {
                _enumeratorCts.Cancel(throwOnFirstException: false);
            }

            try
            {
                _parallelTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (wasEarlyExit && _enumeratorCts.IsCancellationRequested)
            {
                // swallow early exit cancellations
            }
            finally
            {
                _semaphore.Dispose();
                _pipelineCts.Dispose();
                _enumeratorCts.Dispose();
            }
        }

        public void Reset() => throw new NotSupportedException();

        private sealed class Consumer : IConsumer<SlimList<T>>
        {
            private readonly ConcurrentQueue<SlimList<T>> _queue;
            private readonly SemaphoreSlim _semaphore;
            private readonly CancellationToken _cancellationToken;

            public Consumer(
                ConcurrentQueue<SlimList<T>> queue,
                SemaphoreSlim semaphore,
                CancellationToken cancellationToken
            )
            {
                _queue = queue;
                _semaphore = semaphore;
                _cancellationToken = cancellationToken;
            }

            public void Consume(SlimList<T> state, Exception? ex)
            {
                if (ex is null && !_cancellationToken.IsCancellationRequested)
                {
                    _queue.Enqueue(state);
                    _semaphore.Release();
                }
            }

            public ValueTask ConsumeAsync(SlimList<T> state, Exception? ex, CancellationToken cancellationToken)
            {
                return ValueTask.FromException(new UnreachableException());
            }
        }
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
        /// <summary>
        /// Channel that buffers the items not yet consumed by the enumerator.
        /// </summary>
        private readonly Channel<SlimList<T>> _channel;

        /// <summary>Token to cancel the background tasks if enumeration ends early (e.g. via <c>break</c>)</summary>
        private readonly CancellationTokenSource _enumeratorCts;

        /// <summary>Combined token for the entire pipeline (user + enumerator), early exit, user cancellation, or exception</summary>
        private readonly CancellationTokenSource _pipelineCts;

        /// <summary>Task running the parallel operation in the background</summary>
        private readonly Task _parallelTask;

        public ArraySegment<T> Current => _current!.AsArraySegment();

        private SlimList<T>? _current;
        private bool _done;

        public ParallelAsyncEnumerator(
            Func<IConsumer<SlimList<T>>, CancellationToken, Task> parallelForEachAsync,
            CancellationToken userToken,
            CancellationToken getEnumeratorToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();
            _pipelineCts = CancellationTokenSource.CreateLinked(userToken, getEnumeratorToken, _enumeratorCts.Token);

            _channel = Channel.CreateUnbounded<SlimList<T>>(
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
                        await parallelForEachAsync(new Consumer(_channel.Writer), _pipelineCts.Token)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _channel.Writer.Complete();
                    }
                },
                CancellationToken.None
            );

            _done = false;
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
            while (await _channel.Reader.WaitToReadAsync(_pipelineCts.Token).ConfigureAwait(false))
            {
                if (_channel.Reader.TryRead(out _current))
                {
                    return true;
                }
            }

            _current = null;
            Volatile.Write(ref _done, true);
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            // if _done is not already true, we are exiting early via "break" in a foreach or similar
            bool wasEarlyExit = !Interlocked.Exchange(ref _done, true);

            // If we haven't observed a terminal state yet, assume the consumer is quitting early -> signal our side.
            if (wasEarlyExit)
            {
                _enumeratorCts.Cancel(throwOnFirstException: false);
            }

            try
            {
                await _parallelTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (wasEarlyExit && _enumeratorCts.IsCancellationRequested)
            {
                // Enumerator early-exit -> swallow
            }
            finally
            {
                _pipelineCts.Dispose();
                _enumeratorCts.Dispose();
            }
        }

        private sealed class Consumer(ChannelWriter<SlimList<T>> channel) : IConsumer<SlimList<T>>
        {
            public void Consume(SlimList<T> state, Exception? ex)
            {
                throw new UnreachableException();
            }

            public ValueTask ConsumeAsync(SlimList<T> state, Exception? ex, CancellationToken cancellationToken)
            {
                return ex is null ? channel.WriteAsync(state, cancellationToken)
                    : cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken)
                    : ValueTask.CompletedTask;
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

            Check.True(third.CanBeCanceled, "The third token must be cancellable.");
            return CancellationTokenSource.CreateLinkedTokenSource(first, second, third);
        }
    }
}
