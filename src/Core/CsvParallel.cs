using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Binding;
using FlameCsv.IO.Internal;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Writing;

namespace FlameCsv;

internal static class CsvParallel
{
    public const int DefaultChunkSize = 128;

    /// <summary>
    /// Options for parallel CSV processing.
    /// </summary>
    public readonly record struct Options
    {
        /// <summary>
        /// Size of chunks to use when processing data in parallel.<br/>
        /// When reading CSV, this many records are parsed before they are yielded to the consumer.<br/>
        /// Less critical when writing, the records are batched but the writers are still flushed based on their buffer saturation.<br/>
        /// If unset, defaults to <c>128</c>.
        /// </summary>
        public int? ChunkSize { get; init; }

        /// <summary>
        /// Token to cancel the read or write operation.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>
        /// Maximum degree of parallelism to use when processing CSV data in parallel.<br/>
        /// When reading, the default is <c>4</c> or the number of processors on the machine, whichever is smaller.<br/>
        /// When writing, defaults to the number of processors on the machine, or the one picked by TPL.
        /// </summary>
        public int? MaxDegreeOfParallelism { get; init; }

        internal Options Validated()
        {
            if (ChunkSize.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ChunkSize.Value, "options.ChunkSize");
            }

            if (MaxDegreeOfParallelism.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                    MaxDegreeOfParallelism.Value,
                    "options.MaxDegreeOfParallelism"
                );
            }

            return this;
        }

        internal Options ValidatedForReading()
        {
            var options = Validated();

            if (!options.MaxDegreeOfParallelism.HasValue)
            {
                options = options with { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4) };
            }

            return options;
        }
    }

    public static void ForEach<T, TValue>(
        CsvTypeMap<T, TValue> typeMap,
        IParallelReader<T> reader,
        Action<List<TValue>> action,
        CsvOptions<T>? options = null,
        Options parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.ValidatedForReading();

        ParallelCore<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ValueConsumer<TValue>, List<TValue>>(
            reader,
            new ValueProducer<T, TValue>(
                parallelOptions.ChunkSize ?? DefaultChunkSize,
                options ?? CsvOptions<T>.Default,
                typeMap
            ),
            new ValueConsumer<TValue>(action, null!, parallelOptions.ChunkSize ?? DefaultChunkSize),
            parallelOptions
        );
    }

    public static Task ForEachAsync<T, TValue>(
        CsvTypeMap<T, TValue> typeMap,
        IParallelReader<T> reader,
        Func<List<TValue>, CancellationToken, ValueTask> asyncAction,
        CsvOptions<T>? options = null,
        Options parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.ValidatedForReading();

        return ParallelAsyncCore<
            CsvRecordRef<T>,
            Chunk<T>,
            ValueProducer<T, TValue>,
            ValueConsumer<TValue>,
            List<TValue>
        >(
            reader,
            new ValueProducer<T, TValue>(
                parallelOptions.ChunkSize ?? DefaultChunkSize,
                options ?? CsvOptions<T>.Default,
                typeMap
            ),
            new ValueConsumer<TValue>(null!, asyncAction, parallelOptions.ChunkSize ?? DefaultChunkSize),
            parallelOptions
        );
    }

    internal static void WriteUnordered<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>> sink,
        Options parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.Validated();

        ParallelCore<
            TValue,
            ParallelChunker.HasOrderEnumerable<TValue>,
            CsvWriterProducer<T, TValue>,
            CsvWriterConsumer<T>,
            CsvFieldWriter<T>
        >(
            ParallelChunker.Chunk(source, parallelOptions.ChunkSize ?? DefaultChunkSize),
            new CsvWriterProducer<T, TValue>(options, dematerializer, sink),
            new CsvWriterConsumer<T>(),
            parallelOptions
        );
    }

    internal static Task WriteUnorderedAsync<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> sink,
        Options parallelOptions = default
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        parallelOptions = parallelOptions.Validated();

        return ParallelAsyncCore<
            TValue,
            ParallelChunker.HasOrderEnumerable<TValue>,
            CsvWriterProducer<T, TValue>,
            CsvWriterConsumer<T>,
            CsvFieldWriter<T>
        >(
            ParallelChunker.Chunk(source, parallelOptions.ChunkSize ?? DefaultChunkSize),
            new CsvWriterProducer<T, TValue>(options, dematerializer, sink),
            new CsvWriterConsumer<T>(),
            parallelOptions
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ParallelCore<T, TElement, TProducer, TConsumer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        TConsumer consumer,
        Options parallelOptions
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState>
        where TConsumer : IConsumer<TState>
    {
        using var cts = parallelOptions.CancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken)
            : new CancellationTokenSource();
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
                            consumer.Consume(in state);
                        }
                        catch (Exception e)
                        {
                            consumerException = e;
                            cts.Cancel();
                        }
                        finally
                        {
                            consumer.Finalize(in state, consumerException);
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
                    MaxDegreeOfParallelism = parallelOptions.MaxDegreeOfParallelism ?? -1,
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
                            if (consumer.ShouldConsume(in state))
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

        // this should only contain items on exception
        while (consumerQueue.TryDequeue(out TState? state))
        {
            consumer.Finalize(in state, producerException ?? consumerException);
        }

        try
        {
            if (producerException is not null)
                producer.OnException(producerException);

            if (consumerException is not null)
                consumer.OnException(consumerException);

            consumerTask.GetAwaiter().GetResult();
        }
        finally
        {
            producer.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task ParallelAsyncCore<T, TElement, TProducer, TConsumer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        TConsumer consumer,
        Options parallelOptions
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState>
        where TConsumer : IConsumer<TState>
    {
        using var cts = parallelOptions.CancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken)
            : new CancellationTokenSource();
        CancellationToken cancellationToken = cts.Token;

        cancellationToken.ThrowIfCancellationRequested();

        AsyncLocal<StrongBox<TState>> localState = new(); // per-thread state
        ConcurrentQueue<TState> createdStates = []; // all created states to be finalized later

        Exception? consumerException = null;
        Exception? producerException = null;

        int channelCapacity = Environment.ProcessorCount;

        if (parallelOptions.MaxDegreeOfParallelism.HasValue)
        {
            channelCapacity = Math.Min(parallelOptions.MaxDegreeOfParallelism.Value, channelCapacity);
        }

        Channel<TState> channel = Channel.CreateBounded<TState>(
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
                    while (channel.Reader.TryRead(out TState? state))
                    {
                        try
                        {
                            await consumer.ConsumeAsync(state, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            consumerException ??= e;
                            cts.Cancel();
                            break;
                        }
                        finally
                        {
                            await consumer.FinalizeAsync(state, consumerException).ConfigureAwait(false);
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
                        MaxDegreeOfParallelism = parallelOptions.MaxDegreeOfParallelism ?? -1,
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
                                box = localState.Value = new StrongBox<TState>(producer.CreateState());
                                createdStates.Enqueue(box.Value!);
                            }

                            while (enumerator.MoveNext())
                            {
                                if (consumer.ShouldConsume(in box.Value!))
                                {
                                    await channel
                                        .Writer.WriteAsync(box.Value!, cancellationToken)
                                        .ConfigureAwait(false);
                                    box.Value = producer.CreateState();
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

        while (createdStates.TryDequeue(out TState? state))
        {
            if (exception is null)
            {
                await channel.Writer.WriteAsync(state, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await consumer.FinalizeAsync(state, exception).ConfigureAwait(false);
            }
        }

        try
        {
            channel.Writer.Complete(exception);

            if (producerException is not null)
                producer.OnException(producerException);

            if (consumerException is not null)
                consumer.OnException(consumerException);

            await consumerTask.ConfigureAwait(false);
        }
        finally
        {
            producer.Dispose();
        }
    }
}
