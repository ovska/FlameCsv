using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Extensions;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv;

internal static partial class CsvParallel
{
    internal delegate void Consume<T>(in T value, Exception? exception)
        where T : IConsumable;

    internal delegate ValueTask ConsumeAsync<T>(T value, Exception? exception, CancellationToken cancellationToken)
        where T : IConsumable;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ForEach<T, TElement, TProducer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        Consume<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState, TElement>
        where TState : IConsumable
    {
        cts.Token.ThrowIfCancellationRequested();

        const int MaxQueuedConsumers = 2;
        using SemaphoreSlim producerSignal = new(initialCount: 0); // limits how many producers can be active
        using SemaphoreSlim consumerSignal = new(initialCount: 0); // signals when there are items to consume

        ConcurrentQueue<TState> consumerQueue = [];
        Exception? exception = null;

        long activeProducers = 0;

        producer.BeforeLoop(cts.Token);

        Task consumerTask = Task.Run(
            () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        consumerSignal.Wait(cts.Token);

                        while (!cts.Token.IsCancellationRequested && consumerQueue.TryDequeue(out TState? state))
                        {
                            try
                            {
                                consumer(in state, null);
                            }
                            finally
                            {
                                producerSignal.Release();
                            }
                        }

                        if (Volatile.Read(ref activeProducers) == 0 && consumerQueue.IsEmpty)
                        {
                            break; // All done, queue was empty
                        }
                    }
                }
                catch (Exception e)
                {
                    exception ??= e;
                    cts.Cancel();
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
                    CancellationToken = cts.Token,
                },
                localInit: () =>
                {
                    Interlocked.Increment(ref activeProducers);
                    producerSignal.Release(MaxQueuedConsumers); // make space for writers
                    return producer.CreateState();
                },
                body: (chunk, loopState, _, state) =>
                {
                    try
                    {
                        foreach (var item in chunk)
                        {
                            if (state.ShouldConsume)
                            {
                                consumerQueue.Enqueue(state);
                                consumerSignal.Release(); // signal that consumer should work
                                producerSignal.Wait(cts.Token); // wait until there's space for another producer

                                state = producer.CreateState();
                            }

                            producer.Produce(chunk, item, ref state);
                        }
                    }
                    catch (Exception e)
                    {
                        exception ??= e;
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
            exception ??= e;
        }

        try
        {
            consumerTask.GetAwaiter().GetResult();
        }
        finally
        {
            while (consumerQueue.TryDequeue(out TState? state))
            {
                if (exception is null)
                {
                    consumer(in state, exception);
                }
                else
                {
                    state.Dispose();
                }
            }
        }

        exception?.Rethrow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Task ForEachAsync<T, TElement, TProducer, TState>(
        IEnumerable<TElement> source,
        TProducer producer,
        ConsumeAsync<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState, TElement>
        where TState : IConsumable
    {
        return ForEachAsync<T, TElement, TProducer, TState>(
            new SyncToAsyncEnumerable<TElement>(source),
            producer,
            consumer,
            cts,
            maxDegreeOfParallelism
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task ForEachAsync<T, TElement, TProducer, TState>(
        IAsyncEnumerable<TElement> source,
        TProducer producer,
        ConsumeAsync<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState, TElement>
        where TState : IConsumable
    {
        cts.Token.ThrowIfCancellationRequested();

        Exception? exception = null;
        ConcurrentStack<TState> unconsumedStates = [];

        int channelCapacity = Environment.ProcessorCount;
        channelCapacity = Math.Min(maxDegreeOfParallelism ?? channelCapacity, channelCapacity);

        Channel<TState> channel = Channel.CreateBounded<TState>(
            new BoundedChannelOptions(channelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );

        await producer.BeforeLoopAsync(cts.Token).ConfigureAwait(false);

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
                            await consumer(state, exception, cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            exception ??= e;
                            cts.Cancel();
                            throw;
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
                        CancellationToken = cts.Token,
                    },
                    body: async (chunk, innerToken) =>
                    {
                        var enumerator = chunk.GetEnumerator();

                        try
                        {
                            if (!unconsumedStates.TryPop(out TState? state))
                            {
                                state = producer.CreateState();
                            }

                            while (enumerator.MoveNext())
                            {
                                if (state.ShouldConsume)
                                {
                                    await channel.Writer.WriteAsync(state, innerToken).ConfigureAwait(false);
                                    state = producer.CreateState();
                                }

                                producer.Produce(chunk, enumerator.Current, ref state);
                            }

                            // state was left unfinalized
                            unconsumedStates.Push(state);
                        }
                        catch (Exception ex)
                        {
                            exception ??= ex;
                            cts.Cancel();
                            channel.Writer.TryComplete(ex);
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
            exception ??= e;
        }

        // flush remaining states
        while (unconsumedStates.TryPop(out TState? remaining))
        {
            if (exception is null)
            {
                await channel.Writer.WriteAsync(remaining, cts.Token).ConfigureAwait(false);
            }
            else
            {
                remaining.Dispose();
            }
        }

        channel.Writer.TryComplete(exception);

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        finally
        {
            exception?.Rethrow();
        }
    }

    public static async Task WriteToChannel<T, TValue>(
        ChannelWriter<TValue> channelWriter,
        Csv.IParallelReadBuilder<T> builder,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CancellationToken cancellationToken = builder.ParallelOptions.CancellationToken;
        int maxDegreeOfParallelism = builder.ParallelOptions.ReadingMaxDegreeOfParallelism;
        int chunkSize = builder.ParallelOptions.EffectiveChunkSize;

        producer.BeforeLoop(cancellationToken);

        try
        {
            await Parallel
                .ForEachAsync(
                    builder.CreateParallelReader(producer.Options, isAsync: true).AsAsyncEnumerable(),
                    parallelOptions: new()
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    },
                    async (chunk, innerToken) =>
                    {
                        foreach (CsvRecordRef<T> record in chunk)
                        {
                            if (producer.TryProduceDirect(chunk, record, out TValue? value))
                            {
                                await channelWriter.WriteAsync(value!, innerToken).ConfigureAwait(false);
                            }
                        }
                    }
                )
                .ConfigureAwait(false);

            channelWriter.Complete();
        }
        catch (Exception ex)
        {
            channelWriter.Complete(ex);
        }
    }
}
