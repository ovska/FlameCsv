using FlameCsv.Binding;
using FlameCsv.IO;
using FlameCsv.ParallelUtils;
using FlameCsv.Writing;

namespace FlameCsv;

static partial class Csv
{
    private sealed class ParallelWriteWrapper<T, TSelf>(
        IWriteBuilderBase<T, TSelf> inner,
        CsvParallelOptions parallelOptions
    ) : IParallelWriteBuilder<T>
        where T : unmanaged, IBinaryInteger<T>
        where TSelf : IWriteBuilderBase<T, TSelf>
    {
        public CsvIOOptions IOOptions => inner.IOOptions;

        public CsvParallelOptions ParallelOptions => parallelOptions;

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask> flushAction
        )
        {
            return inner.CreateAsyncParallelWriter(out flushAction);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<T>, bool> flushAction)
        {
            return inner.CreateParallelWriter(out flushAction);
        }

        public ICsvBufferWriter<T> CreateWriter(bool isAsync)
        {
            return inner.CreateWriter(isAsync);
        }

        public IParallelWriteBuilder<T> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ParallelWriteWrapper<T, TSelf>(inner.WithIOOptions(ioOptions), ParallelOptions);
        }

        public IParallelWriteBuilder<T> WithParallelOptions(in CsvParallelOptions parallelOptions)
        {
            return new ParallelWriteWrapper<T, TSelf>(inner, parallelOptions);
        }
    }

    /// <summary>
    /// Builder to create a parallel CSV writing pipeline from.
    /// </summary>
    public interface IParallelWriteBuilder<T> : IWriteBuilderBase<T, IParallelWriteBuilder<T>>
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
        IParallelWriteBuilder<T> WithParallelOptions(in CsvParallelOptions parallelOptions);

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
        /// Records are serialized in parallel. Order depends on <see cref="CsvParallelOptions.Unordered"/>.
        /// </summary>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        void Write<[DAM(Messages.ReflectionBound)] TValue>(IEnumerable<TValue> values, CsvOptions<T>? options = null)
        {
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            Util.WriteCore(values, options, options.TypeBinder.GetDematerializer<TValue>(), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using the type map.
        /// Records are serialized in parallel. Order depends on <see cref="CsvParallelOptions.Unordered"/>.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        void Write<TValue>(CsvTypeMap<T, TValue> typeMap, IEnumerable<TValue> values, CsvOptions<T>? options = null)
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;

            Util.WriteCore(values, options, typeMap.GetDematerializer(options), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
        /// Records are serialized in parallel. Order depends on <see cref="CsvParallelOptions.Unordered"/>.
        /// </summary>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>A task that completes when writing has finished</returns>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            IEnumerable<TValue> values,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            return Util.WriteAsyncCore(values, options, options.TypeBinder.GetDematerializer<TValue>(), this);
        }

        /// <inheritdoc cref="WriteAsync{TValue}(IEnumerable{TValue}, CsvOptions{T}?)"/>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            IAsyncEnumerable<TValue> values,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            return Util.WriteAsyncCore(values, options, options.TypeBinder.GetDematerializer<TValue>(), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using the type map.
        /// Records are serialized in parallel. Order depends on <see cref="CsvParallelOptions.Unordered"/>.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>A task that completes when writing has finished</returns>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            CsvTypeMap<T, TValue> typeMap,
            IEnumerable<TValue> values,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            return Util.WriteAsyncCore(values, options, typeMap.GetDematerializer(options), this);
        }

        /// <inheritdoc cref="WriteAsync{TValue}(IEnumerable{TValue}, CsvOptions{T}?)"/>
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            CsvTypeMap<T, TValue> typeMap,
            IAsyncEnumerable<TValue> values,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            return Util.WriteAsyncCore(values, options, typeMap.GetDematerializer(options), this);
        }
    }
}

file static class Util
{
    internal static void WriteCore<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;
        parallelOptions.CancellationToken.ThrowIfCancellationRequested();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);

        using (builder.CreateParallelWriter(out Action<ReadOnlySpan<T>, bool> sink))
        {
            CsvParallel
                .RunAsync<
                    TValue,
                    ParallelChunker.HasOrderEnumerable<TValue>,
                    CsvWriterProducer<T, TValue, ParallelChunker.HasOrderEnumerable<TValue>>,
                    CsvFieldWriter<T>
                >(
                    ParallelChunker.Chunk(source, parallelOptions.EffectiveChunkSize),
                    new(options, builder.IOOptions, dematerializer, sink),
                    CsvWriterConsumer<T>.Instance,
                    cts,
                    parallelOptions,
                    isAsync: false
                )
                .GetAwaiter()
                .GetResult();
        }
    }

    internal static async Task WriteAsyncCore<T, TValue>(
        object source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;
        parallelOptions.CancellationToken.ThrowIfCancellationRequested();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);

        await using (
            builder.CreateAsyncParallelWriter(out Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask> sink)
        )
        {
            await CsvParallel
                .RunAsync<
                    TValue,
                    ParallelChunker.HasOrderEnumerable<TValue>,
                    CsvWriterProducer<T, TValue, ParallelChunker.HasOrderEnumerable<TValue>>,
                    CsvFieldWriter<T>
                >(
                    ParallelChunker.ChunkUnknown<TValue>(source, parallelOptions.EffectiveChunkSize),
                    new(options, builder.IOOptions, dematerializer, sink),
                    CsvWriterConsumer<T>.Instance,
                    cts,
                    parallelOptions,
                    isAsync: true
                )
                .ConfigureAwait(false);
        }
    }
}
