using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Extensions;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;

namespace FlameCsv;

internal static partial class CsvParallel
{
    internal delegate void Consume<T>(in T value, Exception? exception)
        where T : IConsumable;

    internal delegate ValueTask ConsumeAsync<T>(T value, Exception? exception, CancellationToken cancellationToken)
        where T : IConsumable;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task ForEachAsync<T, TElement, TProducer, TState>(
        object source,
        TProducer producer,
        IConsumer<TState> consumer,
        CancellationTokenSource cts,
        int? maxDegreeOfParallelism,
        bool isAsync
    )
        where T : allows ref struct
        where TElement : IEnumerable<T>, IHasOrder
        where TProducer : IProducer<T, TState, TElement>
        where TState : IConsumable
    {
        Check.True(
            source is IEnumerable<TElement> or IAsyncEnumerable<TElement>,
            $"Source must be enumerable, was: {source.GetType()}"
        );

        cts.Token.ThrowIfCancellationRequested();

        if (isAsync)
        {
            await producer.BeforeLoopAsync(cts.Token).ConfigureAwait(false);
        }
        else
        {
            producer.BeforeLoop(cts.Token);
        }

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

        Task consumerTask = Task.Run(ConsumerCallback, CancellationToken.None);

        try
        {
            ParallelOptions tplOpts = new()
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism ?? -1,
                CancellationToken = cts.Token,
            };

            Task producerTask = source is IAsyncEnumerable<TElement> asyncSource
                ? Parallel.ForEachAsync(asyncSource, tplOpts, ProducerCallback)
                : Parallel.ForEachAsync((IEnumerable<TElement>)source, tplOpts, ProducerCallback);

            await producerTask.ConfigureAwait(false);
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

        async Task ConsumerCallback()
        {
            while (
                !cts.IsCancellationRequested && await channel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false)
            )
            {
                while (channel.Reader.TryRead(out TState? state))
                {
                    try
                    {
                        if (isAsync)
                        {
                            await consumer.ConsumeAsync(state, exception, cts.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            consumer.Consume(in state, exception);
                        }
                    }
                    catch (Exception e)
                    {
                        exception ??= e;
                        cts.Cancel();
                        throw;
                    }
                }
            }
        }

        async ValueTask ProducerCallback(TElement chunk, CancellationToken innerToken)
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
