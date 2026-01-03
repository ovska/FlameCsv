using System.Threading.Channels;
using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;

namespace FlameCsv;

static partial class Csv
{
    /// <summary>
    /// Builder to create a parallel CSV reading pipeline from.
    /// </summary>
    public interface IParallelReadBuilder<T> : IReadBuilderBase<T, IParallelReadBuilder<T>>
        where T : unmanaged, IBinaryInteger<T>
    {
        /// <summary>
        /// Options to use for parallel reading; chunk size, degree of parallelism, and cancellation token.
        /// </summary>
        CsvParallelOptions ParallelOptions { get; }

        /// <summary>
        /// Configures the builder to use the given parallel options.
        /// </summary>
        /// <param name="parallelOptions">Options to use for parallel reading</param>
        IParallelReadBuilder<T> WithParallelOptions(in CsvParallelOptions parallelOptions);

        /// <summary>
        /// Reads CSV records in parallel and returns them as batches, binding them using reflection.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>An enumerable to read CSV records in parallel</returns>
        /// <remarks>
        /// The batches returned by the enumerator <strong>must not</strong> be held onto after the next iteration,
        /// or after the enumerator is disposed. Process the data in-place, or copy it to another buffer.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public IEnumerable<ArraySegment<TValue>> ReadUnordered<[DAM(Messages.ReflectionBound)] TValue>(
            CsvOptions<T>? options = null
        )
        {
            return Util.AsEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(options ?? CsvOptions<T>.Default, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel and returns them as batches,, binding them using the given type map.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>An enumerable to read CSV records in parallel</returns>
        /// <remarks>
        /// The batches returned by the enumerator <strong>must not</strong> be held onto after the next iteration,
        /// or after the enumerator is disposed. Process the data in-place, or copy it to another buffer.
        /// </remarks>
        public IEnumerable<ArraySegment<TValue>> ReadUnordered<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);

            return Util.AsEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(typeMap, options, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel and returns them as batches, binding them using reflection.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>An enumerable to read CSV records in parallel</returns>
        /// <remarks>
        /// The batches returned by the enumerator <strong>must not</strong> be held onto after the next iteration,
        /// or after the enumerator is disposed. Process the data in-place, or copy it to another buffer.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public IAsyncEnumerable<ArraySegment<TValue>> ReadUnorderedAsync<[DAM(Messages.ReflectionBound)] TValue>(
            CsvOptions<T>? options = null
        )
        {
            return Util.AsAsyncEnumerableCore(options, this, ValueProducer<T, TValue>.Create(options, ParallelOptions));
        }

        /// <summary>
        /// Reads CSV records in parallel and returns them as batches, binding them using the given type map.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>An enumerable to read CSV records in parallel</returns>
        /// <remarks>
        /// The batches returned by the enumerator <strong>must not</strong> be held onto after the next iteration,
        /// or after the enumerator is disposed. Process the data in-place, or copy it to another buffer.
        /// </remarks>
        public IAsyncEnumerable<ArraySegment<TValue>> ReadUnorderedAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);

            return Util.AsAsyncEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(typeMap, options, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using reflection, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public void ForEachUnordered<[DAM(Messages.ReflectionBound)] TValue>(
            Action<ArraySegment<TValue>> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            Util.ForEachCore(options, this, action, ValueProducer<T, TValue>.Create(options, ParallelOptions));
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using reflection, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task ForEachUnorderedAsync<[DAM(Messages.ReflectionBound)] TValue>(
            Func<ArraySegment<TValue>, CancellationToken, ValueTask> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            return Util.ForEachAsyncCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(options, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using the given type map, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map to use for binding</param>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        public void ForEachUnordered<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            Action<ArraySegment<TValue>> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            Util.ForEachCore(options, this, action, ValueProducer<T, TValue>.Create(typeMap, options, ParallelOptions));
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using the given type map, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map to use for binding</param>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>A task that completes when all values have been written to the channel</returns>
        public Task ForEachUnorderedAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            Func<ArraySegment<TValue>, CancellationToken, ValueTask> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(action);
            return Util.ForEachAsyncCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(typeMap, options, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using reflection, and writes them to the given channel.
        /// </summary>
        /// <remarks>
        /// Cancellation token can be passed to <c>AsParallel()</c>.
        /// </remarks>
        /// <param name="channelWriter">Channel to write the values to</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>A task that completes when all values have been written to the channel</returns>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task ToChannelAsync<[DAM(Messages.ReflectionBound)] TValue>(
            ChannelWriter<TValue> channelWriter,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(channelWriter);
            return CsvParallel.WriteToChannel(
                channelWriter,
                this,
                ValueProducer<T, TValue>.Create(options, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using the given type map, and writes them to the given channel.
        /// </summary>
        /// <remarks>
        /// Cancellation token can be passed to <c>AsParallel()</c>.
        /// </remarks>
        /// <param name="channelWriter">Channel to write the values to</param>
        /// <param name="typeMap">Type map to use for binding</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        public Task ToChannelAsync<TValue>(
            ChannelWriter<TValue> channelWriter,
            CsvTypeMap<T, TValue> typeMap,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(channelWriter);
            ArgumentNullException.ThrowIfNull(typeMap);
            return CsvParallel.WriteToChannel(
                channelWriter,
                this,
                ValueProducer<T, TValue>.Create(typeMap, options, ParallelOptions)
            );
        }
    }

    private sealed class ReadParallelBuilder<T, TInner>(
        IReadBuilderBase<T, TInner> inner,
        CsvParallelOptions parallelOptions
    ) : IParallelReadBuilder<T>
        where T : unmanaged, IBinaryInteger<T>
        where TInner : IReadBuilderBase<T, TInner>
    {
        public CsvParallelOptions ParallelOptions => parallelOptions;
        public CsvIOOptions IOOptions => inner.IOOptions;

        public ICsvBufferReader<T> CreateReader(bool isAsync)
        {
            return inner.CreateReader(isAsync);
        }

        public IParallelReadBuilder<T> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadParallelBuilder<T, TInner>(inner.WithIOOptions(ioOptions), ParallelOptions);
        }

        public IParallelReadBuilder<T> WithParallelOptions(in CsvParallelOptions parallelOptions)
        {
            return new ReadParallelBuilder<T, TInner>(inner, parallelOptions);
        }

        IParallelReader<T> IReadBuilderBase<T, IParallelReadBuilder<T>>.CreateParallelReader(
            CsvOptions<T> options,
            bool isAsync
        )
        {
            return inner.CreateParallelReader(options, isAsync);
        }
    }
}

file static class Util
{
    public static IEnumerable<ArraySegment<TValue>> AsEnumerableCore<T, TValue>(
        CsvOptions<T>? options,
        Csv.IParallelReadBuilder<T> builder,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        return new CsvParallel.ParallelEnumerable<TValue>(
            (consume, innerToken) =>
            {
                using var reader = builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: false);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken);

                CsvParallel.ForEach<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, SlimList<TValue>>(
                    reader.AsEnumerable(),
                    producer,
                    consume,
                    cts,
                    parallelOptions.ReadingMaxDegreeOfParallelism
                );
            },
            parallelOptions.CancellationToken
        );
    }

    public static IAsyncEnumerable<ArraySegment<TValue>> AsAsyncEnumerableCore<T, TValue>(
        CsvOptions<T>? options,
        Csv.IParallelReadBuilder<T> builder,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        return new CsvParallel.ParallelAsyncEnumerable<TValue>(
            async (consumeAsync, innerToken) =>
            {
                var reader = builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: true);

                await using (reader.ConfigureAwait(false))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken))
                {
                    await CsvParallel
                        .ForEachAsync<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, SlimList<TValue>>(
                            reader.AsAsyncEnumerable(),
                            producer,
                            consumeAsync,
                            cts,
                            parallelOptions.ReadingMaxDegreeOfParallelism
                        )
                        .ConfigureAwait(false);
                }
            },
            parallelOptions.CancellationToken
        );
    }

    public static void ForEachCore<T, TValue>(
        CsvOptions<T>? options,
        Csv.IParallelReadBuilder<T> builder,
        Action<ArraySegment<TValue>> action,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        using var reader = builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);
        CsvParallel.ForEach<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, SlimList<TValue>>(
            reader.AsEnumerable(),
            producer,
            (in chunk, ex) =>
            {
                if (ex is null)
                {
                    action(chunk.AsArraySegment());
                }
            },
            cts,
            parallelOptions.ReadingMaxDegreeOfParallelism
        );
    }

    public static async Task ForEachAsyncCore<T, TValue>(
        CsvOptions<T>? options,
        Csv.IParallelReadBuilder<T> builder,
        Func<ArraySegment<TValue>, CancellationToken, ValueTask> action,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        var reader = builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: true);

        await using (reader.ConfigureAwait(false))
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken))
        {
            await CsvParallel
                .ForEachAsync<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, SlimList<TValue>>(
                    reader.AsAsyncEnumerable(),
                    producer,
                    (chunk, ex, ct) => ex is null ? action(chunk.AsArraySegment(), ct) : ValueTask.CompletedTask,
                    cts,
                    parallelOptions.ReadingMaxDegreeOfParallelism
                )
                .ConfigureAwait(false);
        }
    }
}
