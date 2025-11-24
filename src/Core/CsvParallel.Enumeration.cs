using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Channels;
using FlameCsv.Binding;
using FlameCsv.IO.Internal;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;

namespace FlameCsv;

public static partial class CsvParallel
{
    public static IEnumerable<ReadOnlySpan<TValue>> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        CsvParallelOptions parallelOptions = default
    )
    {
        return AsEnumerableCore(
            ParallelReader.Create(csv, options ?? CsvOptions<char>.Default),
            ValueProducer<char, TValue>.Create(typeMap, options, parallelOptions),
            parallelOptions
        );
    }

    public static IAsyncEnumerable<ReadOnlyMemory<TValue>> ReadAsync<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        CsvParallelOptions parallelOptions = default
    )
    {
        return AsAsyncEnumerableCore(
            ParallelReader.Create(csv, options ?? CsvOptions<char>.Default),
            ValueProducer<char, TValue>.Create(typeMap, options, parallelOptions),
            parallelOptions
        );
    }

    private static ParallelEnumerable<TValue> AsEnumerableCore<T, TValue>(
        IParallelReader<T> reader,
        ValueProducer<T, TValue> producer,
        CsvParallelOptions parallelOptions
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.ValidatedForReading();

        return new ParallelEnumerable<TValue>(
            (consume, innerToken) =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken);
                ParallelCore<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                    reader,
                    producer,
                    consume,
                    cts,
                    parallelOptions.MaxDegreeOfParallelism
                );
            },
            parallelOptions.CancellationToken
        );
    }

    private static ParallelAsyncEnumerable<TValue> AsAsyncEnumerableCore<T, TValue>(
        IParallelReader<T> reader,
        ValueProducer<T, TValue> producer,
        CsvParallelOptions parallelOptions
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.ValidatedForReading();

        return new ParallelAsyncEnumerable<TValue>(
            async (consumeAsync, innerToken) =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken);

                await ParallelAsyncCore<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                        reader,
                        producer,
                        consumeAsync,
                        cts,
                        parallelOptions.MaxDegreeOfParallelism
                    )
                    .ConfigureAwait(false);
            },
            parallelOptions.CancellationToken
        );
    }

    private sealed class ParallelEnumerable<T>(
        Action<Consume<ChunkManager<T>>, CancellationToken> runParallel,
        CancellationToken userToken
    ) : IEnumerable<ReadOnlySpan<T>>
    {
        public IEnumerator<ReadOnlySpan<T>> GetEnumerator() => new ParallelEnumerator<T>(runParallel, userToken);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ParallelEnumerator<T> : IEnumerator<ReadOnlySpan<T>>
    {
        private readonly BlockingCollection<ChunkManager<T>> _blockingCollection;
        private ChunkManager<T>? _current;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;
        private bool _done;

        public ParallelEnumerator(
            Action<Consume<ChunkManager<T>>, CancellationToken> parallelForEach,
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
                            (in cm, ex) =>
                            {
                                if (ex is null)
                                {
                                    _blockingCollection.Add(cm, _pipelineToken);
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

        public ReadOnlySpan<T> Current => _current!.GetSpan();
        object? IEnumerator.Current => throw new NotSupportedException();

        public bool MoveNext()
        {
            (_current as IDisposable)?.Dispose(); // return to pool
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

    private sealed class ParallelAsyncEnumerable<T>(
        Func<ConsumeAsync<ChunkManager<T>>, CancellationToken, Task> runParallel,
        CancellationToken userToken
    ) : IAsyncEnumerable<ReadOnlyMemory<T>>
    {
        public IAsyncEnumerator<ReadOnlyMemory<T>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new ParallelAsyncEnumerator<T>(runParallel, userToken, cancellationToken);
        }
    }

    private sealed class ParallelAsyncEnumerator<T> : IAsyncEnumerator<ReadOnlyMemory<T>>
    {
        private readonly Channel<ChunkManager<T>> _channel;

        private readonly CancellationTokenSource _enumeratorCts;
        private readonly CancellationTokenSource _pipelineCts; // user + enumerator
        private readonly CancellationToken _pipelineToken;

        private readonly Task _parallelTask;

        public ReadOnlyMemory<T> Current => _current!.Memory;

        private ChunkManager<T>? _current;

        public ParallelAsyncEnumerator(
            Func<ConsumeAsync<ChunkManager<T>>, CancellationToken, Task> parallelForEachAsync,
            CancellationToken userToken,
            CancellationToken getEnumeratorToken
        )
        {
            _enumeratorCts = new CancellationTokenSource();
            _pipelineCts = CancellationTokenSource.CreateLinked(userToken, getEnumeratorToken, _enumeratorCts.Token);
            _pipelineToken = _pipelineCts.Token;

            _channel = Channel.CreateUnbounded<ChunkManager<T>>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = true,
                }
            );

            _parallelTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await parallelForEachAsync(
                                (cm, ex, ct) =>
                                    ex is null ? _channel.Writer.WriteAsync(cm, ct)
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
            (_current as IDisposable)?.Dispose(); // return to pool

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
