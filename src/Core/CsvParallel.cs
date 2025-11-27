using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Extensions;
using FlameCsv.ParallelUtils;
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

                    if (Volatile.Read(ref activeProducers) == 0 && consumerQueue.IsEmpty)
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
        }
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
        where TProducer : IProducer<T, TState>
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
        where TProducer : IProducer<T, TState>
        where TState : IConsumable
    {
        CancellationToken cancellationToken = cts.Token;

        cancellationToken.ThrowIfCancellationRequested();

        AsyncLocal<StrongBox<TState>> localState = new(); // per-thread state

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
                            await consumer(state.Value!, producerException, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            consumerException ??= e;
                            cts.Cancel();
                            break;
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
                            }

                            while (enumerator.MoveNext())
                            {
                                if (box.Value!.ShouldConsume)
                                {
                                    await channel.Writer.WriteAsync(box, cancellationToken).ConfigureAwait(false);
                                    box = new StrongBox<TState>(producer.CreateState());
                                    localState.Value = box;
                                }

                                producer.Produce(order, enumerator.Current, ref box.Value!);
                            }

                            await channel.Writer.WriteAsync(box, cancellationToken).ConfigureAwait(false);
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

        try
        {
            Exception? exception = producerException ?? consumerException;
            channel.Writer.Complete(exception);
            exception?.Rethrow();
        }
        finally
        {
            await consumerTask.ConfigureAwait(false);
        }
    }
}
