using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Extensions;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;

namespace FlameCsv;

internal static partial class CsvParallel
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static async Task RunAsync<T, TElement, TProducer, TState>(
        object source,
        TProducer producer,
        IConsumer<TState> consumer,
        CancellationTokenSource cts,
        CsvParallelOptions options,
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

        Channel<TState> channel = Channel.CreateBounded<TState>(
            new BoundedChannelOptions(capacity: options.MaxQueuedChunks ?? Environment.ProcessorCount)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );
        ChannelReader<TState> reader = channel.Reader;
        ChannelWriter<TState> writer = channel.Writer;
        IScheduler<TState> scheduler = options.Unordered
            ? new UnorderedScheduler<TState>(writer, cts.Token)
            : new OrderedScheduler<TState>(writer, cts.Token);

        ParallelOptions tplOptions = (ParallelOptions)options;

        Task producerTask = source is IAsyncEnumerable<TElement> asyncSource
            ? Parallel.ForEachAsync(asyncSource, tplOptions, ProducerCallback)
            : Parallel.ForEachAsync((IEnumerable<TElement>)source, tplOptions, ProducerCallback);

        Task consumerTask = Task.Run(isAsync ? AsyncConsumerCallback : SyncConsumerCallback, CancellationToken.None);

        try
        {
            await producerTask.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            exception ??= e;
        }

        try
        {
            // flush remaining states
            if (exception is null)
            {
                await scheduler.FinalizeAsync().ConfigureAwait(false);
            }

            writer.TryComplete(exception);
            await consumerTask.ConfigureAwait(false);
        }
        finally
        {
            scheduler.Dispose();
        }

        exception?.Rethrow();

        async Task SyncConsumerCallback()
        {
            try
            {
                while (await reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                while (reader.TryRead(out TState? state))
                {
                    if (cts.IsCancellationRequested)
                        return;

                    consumer.Consume(state, exception);
                }
            }
            catch (Exception e)
            {
                exception ??= e;
                cts.Cancel();
            }
        }

        async Task AsyncConsumerCallback()
        {
            try
            {
                while (await reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
                while (reader.TryRead(out TState? state))
                {
                    await consumer.ConsumeAsync(state, exception, cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                exception ??= e;
                cts.Cancel();
            }
        }

        async ValueTask ProducerCallback(TElement chunk, CancellationToken innerToken)
        {
            IEnumerator<T> enumerator = chunk.GetEnumerator();

            try
            {
                if (!scheduler.TryGetUnconsumedState(out TState? state))
                {
                    state = producer.CreateState();
                }

                while (enumerator.MoveNext())
                {
                    if (state.ShouldConsume)
                    {
                        await scheduler.ScheduleAsync(state, chunk.Order).ConfigureAwait(false);
                        state = producer.CreateState();
                    }

                    producer.Produce(chunk, enumerator.Current, state);
                }

                // state was left unfinalized, schedule it as last
                await scheduler.ScheduleAsync(state, chunk.Order, isLast: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception ??= ex;
                cts.Cancel();
                writer.TryComplete(ex);
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
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        // async version not needed
        producer.BeforeLoop(parallelOptions.CancellationToken);

        var reader = builder.CreateParallelReader(producer.Options, isAsync: true);

        try
        {
            await Parallel
                .ForEachAsync(
                    reader.AsAsyncEnumerable(),
                    parallelOptions: (ParallelOptions)parallelOptions,
                    async (chunk, innerToken) =>
                    {
                        foreach (CsvRecordRef<T> record in chunk)
                        {
                            if (producer.TryProduce(chunk, record, out TValue? value))
                            {
                                await channelWriter.WriteAsync(value, innerToken).ConfigureAwait(false);
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
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }
}
