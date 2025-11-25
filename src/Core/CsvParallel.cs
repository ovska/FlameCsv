using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Writing;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading and writing CSV data in parallel.<br/>
/// <strong>This API is experimental and may change or be removed in future releases.</strong>
/// </summary>
/// <remarks>
/// All operations are currently <strong>unordered</strong>, and the record order is not guaranteed.
/// </remarks>
public static partial class CsvParallel
{
    internal delegate void Consume<T>(in T value, Exception? exception)
        where T : IConsumable;

    internal delegate ValueTask ConsumeAsync<T>(T value, Exception? exception, CancellationToken cancellationToken)
        where T : IConsumable;

    /// <summary>
    /// Default chunk size for parallel CSV processing.
    /// </summary>
    public const int DefaultChunkSize = 128;

    internal static void ForEach<T, TValue>(
        IParallelReader<T> reader,
        ValueProducer<T, TValue> producer,
        Consume<ChunkManager<TValue>> consume,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ParallelCore<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
            reader,
            producer,
            consume,
            cts,
            maxDegreeOfParallelism
        );
    }

    internal static async Task ForEachAsync<T, TValue>(
        IParallelReader<T> reader,
        ValueProducer<T, TValue> producer,
        ConsumeAsync<ChunkManager<TValue>> consumeAsync,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        await ParallelAsyncCore<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                reader,
                producer,
                consumeAsync,
                cts,
                maxDegreeOfParallelism
            )
            .ConfigureAwait(false);
    }

    internal static void WriteUnordered<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        CsvIOOptions ioOptions,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>> sink,
        CsvParallelOptions parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.Validated();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);

        ParallelCore<
            TValue,
            ParallelChunker.HasOrderEnumerable<TValue>,
            CsvWriterProducer<T, TValue>,
            CsvFieldWriter<T>
        >(
            ParallelChunker.Chunk(source, parallelOptions.ChunkSize ?? DefaultChunkSize),
            new CsvWriterProducer<T, TValue>(options, ioOptions, dematerializer, sink),
            CsvWriterProducer<T>.Consume,
            cts,
            parallelOptions.MaxDegreeOfParallelism
        );
    }

    internal static async Task WriteUnorderedAsync<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> sink,
        CsvParallelOptions parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.Validated();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);

        await ParallelAsyncCore<
            TValue,
            ParallelChunker.HasOrderEnumerable<TValue>,
            CsvWriterProducer<T, TValue>,
            CsvFieldWriter<T>
        >(
                ParallelChunker.Chunk(source, parallelOptions.ChunkSize ?? DefaultChunkSize),
                new CsvWriterProducer<T, TValue>(options, dematerializer, sink),
                CsvWriterProducer<T>.ConsumeAsync,
                cts,
                parallelOptions.MaxDegreeOfParallelism
            )
            .ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ParallelCore<T, TElement, TProducer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        Consume<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState>
        where TState : IConsumable
    {
        CancellationToken cancellationToken = cts.Token;

        cancellationToken.ThrowIfCancellationRequested();

        const int MaxQueuedConsumers = 2;
        using SemaphoreSlim producerSignal = new(initialCount: 0); // limits how many producers can be active
        using SemaphoreSlim consumerSignal = new(initialCount: 0); // signals when there are items to consume

        ConcurrentQueue<TState> consumerQueue = [];
        Exception? consumerException = null;
        Exception? producerException = null;

        long activeProducers = 0;

        producer.BeforeLoop();

        Task consumerTask = Task.Run(
            () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    consumerSignal.Wait(cancellationToken);

                    while (!cancellationToken.IsCancellationRequested && consumerQueue.TryDequeue(out TState? state))
                    {
                        try
                        {
                            consumer(in state, null);
                        }
                        catch (Exception e)
                        {
                            consumerException = e;
                            cts.Cancel();
                        }
                        finally
                        {
                            producerSignal.Release();
                        }
                    }

                    if (Volatile.Read(in activeProducers) == 0)
                    {
                        break; // All done, queue was empty
                    }
                }
            },
            CancellationToken.None
        );

        try
        {
            Parallel.ForEach(
                source: source,
                parallelOptions: new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism ?? -1,
                    CancellationToken = cancellationToken,
                },
                localInit: () =>
                {
                    producerSignal.Release(MaxQueuedConsumers); // make space for writers
                    Interlocked.Increment(ref activeProducers);
                    return producer.CreateState();
                },
                body: (chunk, loopState, _, state) =>
                {
                    try
                    {
                        int order = chunk.Order;

                        foreach (var item in chunk)
                        {
                            if (state.ShouldConsume)
                            {
                                consumerQueue.Enqueue(state);
                                consumerSignal.Release(); // signal that consumer should work
                                producerSignal.Wait(cancellationToken); // wait until there's space for another producer
                                state = producer.CreateState();
                            }

                            producer.Produce(order, item, ref state);
                        }
                    }
                    catch (Exception e)
                    {
                        producerException = e;
                        loopState.Stop();
                        cts.Cancel();
                    }

                    return state;
                },
                localFinally: state =>
                {
                    consumerQueue.Enqueue(state);
                    consumerSignal.Release(); // signal that consumer should work

                    if (Interlocked.Decrement(ref activeProducers) == 0)
                    {
                        consumerSignal.Release(); // Wake up consumer to check completion
                    }
                }
            );
        }
        catch (Exception e)
        {
            producerException ??= e;
        }

        try
        {
            consumerTask.GetAwaiter().GetResult();
        }
        finally
        {
            // this should only contain items on exception
            while (consumerQueue.TryDequeue(out TState? state))
            {
                Debug.Assert(
                    producerException is not null || consumerException is not null,
                    "There should be an exception if there are remaining items in the consumer queue"
                );

                consumer(in state, producerException ?? consumerException);
            }

            producer.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task ParallelAsyncCore<T, TElement, TProducer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        ConsumeAsync<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState>
        where TState : IConsumable
    {
        CancellationToken cancellationToken = cts.Token;

        cancellationToken.ThrowIfCancellationRequested();

        AsyncLocal<StrongBox<TState>> localState = new(); // per-thread state
        ConcurrentDictionary<StrongBox<TState>, object?> unfinalizedStates = [];

        Exception? consumerException = null;
        Exception? producerException = null;

        int channelCapacity = Environment.ProcessorCount;
        channelCapacity = Math.Min(maxDegreeOfParallelism ?? channelCapacity, channelCapacity);

        Channel<StrongBox<TState>> channel = Channel.CreateBounded<StrongBox<TState>>(
            new BoundedChannelOptions(channelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );

        await producer.BeforeLoopAsync(cancellationToken).ConfigureAwait(false);

        Task consumerTask = Task.Run(
            async () =>
            {
                while (
                    !cts.IsCancellationRequested
                    && await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)
                )
                {
                    while (channel.Reader.TryRead(out StrongBox<TState>? state))
                    {
                        try
                        {
                            await consumer(state.Value!, null, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            consumerException ??= e;
                            cts.Cancel();
                            break;
                        }
                        finally
                        {
                            unfinalizedStates.TryRemove(state, out _);
                        }
                    }
                }
            },
            CancellationToken.None
        );

        try
        {
            await Parallel
                .ForEachAsync(
                    source,
                    parallelOptions: new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxDegreeOfParallelism ?? -1,
                        CancellationToken = cancellationToken,
                    },
                    body: async (chunk, cancellationToken) =>
                    {
                        var enumerator = chunk.GetEnumerator();
                        int order = chunk.Order;

                        try
                        {
                            StrongBox<TState>? box = localState.Value;

                            if (box is null)
                            {
                                box = new StrongBox<TState>(producer.CreateState());
                                localState.Value = box;
                                unfinalizedStates.TryAdd(box, null);
                            }

                            while (enumerator.MoveNext())
                            {
                                if (box.Value!.ShouldConsume)
                                {
                                    await channel.Writer.WriteAsync(box, cancellationToken).ConfigureAwait(false);
                                    box = new StrongBox<TState>(producer.CreateState());
                                    localState.Value = box;
                                    unfinalizedStates.TryAdd(box, null);
                                }

                                producer.Produce(order, enumerator.Current, ref box.Value!);
                            }
                        }
                        catch
                        {
                            cts.Cancel();
                            throw;
                        }
                        finally
                        {
                            enumerator.Dispose();
                        }
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            producerException ??= e;
        }

        Exception? exception = producerException ?? consumerException;

        foreach (var (box, _) in unfinalizedStates)
        {
            if (exception is null)
            {
                await channel.Writer.WriteAsync(box, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await consumer(box.Value!, exception, CancellationToken.None).ConfigureAwait(false);
            }
        }

        try
        {
            channel.Writer.Complete(exception);
            await consumerTask.ConfigureAwait(false);
        }
        finally
        {
            producer.Dispose();
        }
    }
}
