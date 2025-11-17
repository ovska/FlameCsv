using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.IO;
using FlameCsv.ParallelUtils;
using FlameCsv.Writing;

namespace FlameCsv;

internal static class CsvParallel
{
    public static ParallelLoopResult ForEach<T>(
        ICsvBufferReader<T> reader,
        Action<CsvRecord<T>> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new NotSupportedException();
    }

    public static Task ForEachAsync<T>(
        ICsvBufferReader<T> reader,
        Func<CsvRecord<T>, CancellationToken, ValueTask> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new NotSupportedException();
    }

    internal static void WriteUnordered<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>> sink,
        int chunkSize = 128,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ParallelCore<TValue, CsvWriterProducer<T, TValue>, CsvWriterConsumer<T>, CsvFieldWriter<T>>(
            source.Chunk(chunkSize),
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
        int chunkSize = 128,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        return ParallelAsyncCore<TValue, CsvWriterProducer<T, TValue>, CsvWriterConsumer<T>, CsvFieldWriter<T>>(
            source.Chunk(chunkSize),
            new CsvWriterProducer<T, TValue>(options, dematerializer, sink),
            new CsvWriterConsumer<T>(),
            parallelOptions
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ParallelCore<T, TProducer, TConsumer, TState>(
        IEnumerable<T[]> source,
        TProducer producer,
        TConsumer consumer,
        ParallelOptions? parallelOptions = null
    )
        where TProducer : struct, IProducer<T, TState>
        where TConsumer : struct, IConsumer<TState>
    {
        parallelOptions?.CancellationToken.ThrowIfCancellationRequested();

        producer.BeforeLoop();

        long faultedOn = -1;

        ConcurrentQueue<TState> consumerQueue = [];
        Exception? consumerException = null;
        Exception? producerException = null;

        const int MaxQueuedConsumers = 2;
        using SemaphoreSlim producerSignal = new(initialCount: 0); // limits how many producers can be active
        using SemaphoreSlim consumerSignal = new(initialCount: 0); // signals when there are items to consume
        using var cts = parallelOptions is { CancellationToken: { CanBeCanceled: true } userToken }
            ? CancellationTokenSource.CreateLinkedTokenSource(userToken)
            : new CancellationTokenSource();

        long activeProducers = 0;

        Task consumerTask = Task.Run(
            () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    consumerSignal.Wait(cts.Token);

                    while (consumerQueue.TryDequeue(out TState? state))
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

        ParallelLoopResult result = Parallel.ForEach(
            source: source,
            parallelOptions: new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelOptions?.MaxDegreeOfParallelism ?? -1,
                TaskScheduler = parallelOptions?.TaskScheduler,
                CancellationToken = cts.Token,
            },
            localInit: () =>
            {
                producerSignal.Release(MaxQueuedConsumers); // make space for writers
                Interlocked.Increment(ref activeProducers);
                return producer.CreateState();
            },
            body: (chunk, loopState, index, state) =>
            {
                try
                {
                    foreach (var item in chunk)
                    {
                        if (consumer.ShouldConsume(in state))
                        {
                            consumerQueue.Enqueue(state);
                            consumerSignal.Release(); // signal that consumer should work
                            producerSignal.Wait(cts.Token); // wait until there's space for another producer
                            state = producer.CreateState();
                        }

                        producer.Produce(item, ref state);
                    }
                }
                catch (Exception e)
                {
                    Interlocked.CompareExchange(ref faultedOn, index, -1);
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

        // this should only contain items on exception
        while (consumerQueue.TryDequeue(out TState? state))
        {
            consumer.Finalize(in state, producerException ?? consumerException);
        }

        consumerTask.GetAwaiter().GetResult();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task ParallelAsyncCore<T, TProducer, TConsumer, TState>(
        IEnumerable<T[]> source,
        TProducer producer,
        TConsumer consumer,
        ParallelOptions? parallelOptions = null
    )
        where TProducer : struct, IProducer<T, TState>
        where TConsumer : struct, IConsumer<TState>
    {
        parallelOptions?.CancellationToken.ThrowIfCancellationRequested();

        await producer.BeforeLoopAsync(parallelOptions?.CancellationToken ?? default).ConfigureAwait(false);

        using var cts = parallelOptions is { CancellationToken: { CanBeCanceled: true } userToken }
            ? CancellationTokenSource.CreateLinkedTokenSource(userToken)
            : new CancellationTokenSource();

        AsyncLocal<StrongBox<TState>> localState = new(); // per-thread state
        ConcurrentQueue<TState> createdStates = []; // all created states to be finalized later

        Exception? consumerException = null;
        Exception? producerException = null;

        int channelCapacity = parallelOptions?.MaxDegreeOfParallelism is int parallelism and > 0
            ? Math.Min(parallelism, Environment.ProcessorCount)
            : Environment.ProcessorCount;

        Channel<TState> channel = Channel.CreateBounded<TState>(
            new BoundedChannelOptions(channelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );

        Task consumerTask = Task.Run(
            async () =>
            {
                while (
                    !cts.IsCancellationRequested
                    && await channel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false)
                )
                {
                    while (channel.Reader.TryRead(out TState? state))
                    {
                        try
                        {
                            await consumer.ConsumeAsync(state, cts.Token).ConfigureAwait(false);
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
                        MaxDegreeOfParallelism = parallelOptions?.MaxDegreeOfParallelism ?? -1,
                        TaskScheduler = parallelOptions?.TaskScheduler,
                        CancellationToken = cts.Token,
                    },
                    body: async (chunk, cancellationToken) =>
                    {
                        try
                        {
                            StrongBox<TState>? box = localState.Value;

                            if (box is null)
                            {
                                box = localState.Value = new StrongBox<TState>(producer.CreateState());
                                createdStates.Enqueue(box.Value!);
                            }

                            foreach (var item in chunk)
                            {
                                if (consumer.ShouldConsume(in box.Value!))
                                {
                                    await channel
                                        .Writer.WriteAsync(box.Value!, cancellationToken)
                                        .ConfigureAwait(false);
                                    box.Value = producer.CreateState();
                                }

                                producer.Produce(item, ref box.Value!);
                            }
                        }
                        catch
                        {
                            cts.Cancel();
                            throw;
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
                await channel.Writer.WriteAsync(state, cts.Token).ConfigureAwait(false);
            }
            else
            {
                await consumer.FinalizeAsync(state, exception).ConfigureAwait(false);
            }
        }

        channel.Writer.Complete(exception);

        if (producerException is not null)
            producer.OnException(producerException);

        if (consumerException is not null)
            consumer.OnException(consumerException);

        await consumerTask.ConfigureAwait(false);
    }
}
