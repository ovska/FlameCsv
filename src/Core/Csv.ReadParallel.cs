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
        public IEnumerable<ReadOnlySpan<TValue>> Read<[DAM(Messages.ReflectionBound)] TValue>(
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
        public IEnumerable<ReadOnlySpan<TValue>> Read<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);

            return Util.AsEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(typeMap, options ?? CsvOptions<T>.Default, ParallelOptions)
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
        public IAsyncEnumerable<ReadOnlyMemory<TValue>> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
            CsvOptions<T>? options = null
        )
        {
            return Util.AsAsyncEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(options ?? CsvOptions<T>.Default, ParallelOptions)
            );
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
        public IAsyncEnumerable<ReadOnlyMemory<TValue>> ReadAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);

            return Util.AsAsyncEnumerableCore(
                options,
                this,
                ValueProducer<T, TValue>.Create(typeMap, options ?? CsvOptions<T>.Default, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using reflection, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// The batches <strong>must not</strong> be held onto after the delegate returns.
        /// Process the data in-place, or copy it to another buffer.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public void ForEach<[DAM(Messages.ReflectionBound)] TValue>(
            Action<ReadOnlySpan<TValue>> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            Util.ForEachCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(options ?? CsvOptions<T>.Default, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using reflection, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// The batches <strong>must not</strong> be held onto after the delegate returns.
        /// Process the data in-place, or copy it to another buffer.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task ForEachAsync<[DAM(Messages.ReflectionBound)] TValue>(
            Func<ReadOnlyMemory<TValue>, CancellationToken, ValueTask> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            return Util.ForEachAsyncCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(options ?? CsvOptions<T>.Default, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using the given type map, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map to use for binding</param>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// The batches <strong>must not</strong> be held onto after the delegate returns.
        /// Process the data in-place, or copy it to another buffer.
        /// </remarks>
        public void ForEach<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            Action<ReadOnlySpan<TValue>> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            Util.ForEachCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(typeMap, options ?? CsvOptions<T>.Default, ParallelOptions)
            );
        }

        /// <summary>
        /// Reads CSV records in parallel, binding them using the given type map, and invokes the given action for each batch.
        /// The batches are not guaranteed to be in any particular order.
        /// </summary>
        /// <param name="typeMap">Type map to use for binding</param>
        /// <param name="action">Action to invoke</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// The batches <strong>must not</strong> be held onto after the delegate returns.
        /// Process the data in-place, or copy it to another buffer.
        /// </remarks>
        public Task ForEachAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            Func<ReadOnlyMemory<TValue>, CancellationToken, ValueTask> action,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(action);
            return Util.ForEachAsyncCore(
                options,
                this,
                action,
                ValueProducer<T, TValue>.Create(typeMap, options ?? CsvOptions<T>.Default, ParallelOptions)
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
    public static IEnumerable<ReadOnlySpan<TValue>> AsEnumerableCore<T, TValue>(
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
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken))
                using (producer)
                {
                    CsvParallel.ForEach<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                        builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: false),
                        producer,
                        consume,
                        cts,
                        parallelOptions.ReadingMaxDegreeOfParallelism
                    );
                }
            },
            parallelOptions.CancellationToken
        );
    }

    public static IAsyncEnumerable<ReadOnlyMemory<TValue>> AsAsyncEnumerableCore<T, TValue>(
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
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(innerToken))
                using (producer)
                {
                    await CsvParallel
                        .ForEachAsync<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                            builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: true),
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
        Action<ReadOnlySpan<TValue>> action,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken))
        using (producer)
        {
            CsvParallel.ForEach<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: false),
                producer,
                (in chunk, ex) =>
                {
                    if (ex is null)
                    {
                        action(chunk.GetSpan());
                    }
                },
                cts,
                parallelOptions.ReadingMaxDegreeOfParallelism
            );
        }
    }

    public static async Task ForEachAsyncCore<T, TValue>(
        CsvOptions<T>? options,
        Csv.IParallelReadBuilder<T> builder,
        Func<ReadOnlyMemory<TValue>, CancellationToken, ValueTask> action,
        ValueProducer<T, TValue> producer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken))
        using (producer)
        {
            await CsvParallel
                .ForEachAsync<CsvRecordRef<T>, Chunk<T>, ValueProducer<T, TValue>, ChunkManager<TValue>>(
                    builder.CreateParallelReader(options ?? CsvOptions<T>.Default, isAsync: true),
                    producer,
                    (chunk, ex, ct) => ex is null ? action(chunk.Memory, ct) : ValueTask.CompletedTask,
                    cts,
                    parallelOptions.ReadingMaxDegreeOfParallelism
                )
                .ConfigureAwait(false);
        }
    }
}
