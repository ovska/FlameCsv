using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.ParallelUtils;
using FlameCsv.Utilities;
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
            out Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> flushAction
        )
        {
            return inner.CreateAsyncParallelWriter(out flushAction);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<T>> flushAction)
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
            Util.WriteUnordered(values, options, options.TypeBinder.GetDematerializer<TValue>(), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
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

            Util.WriteUnordered(values, options, typeMap.GetDematerializer(options), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
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
            return Util.WriteUnorderedAsync(
                new SyncToAsyncEnumerable<TValue>(values),
                options,
                options.TypeBinder.GetDematerializer<TValue>(),
                this
            );
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
            return Util.WriteUnorderedAsync(values, options, options.TypeBinder.GetDematerializer<TValue>(), this);
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
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
            return Util.WriteUnorderedAsync(
                new SyncToAsyncEnumerable<TValue>(values),
                options,
                typeMap.GetDematerializer(options),
                this
            );
        }

        /// <inheritdoc cref="WriteAsync{TValue}(IEnumerable{TValue}, CsvOptions{T}?)"/>
        public Task WriteAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            IAsyncEnumerable<TValue> values,
            CsvOptions<T>? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;
            return Util.WriteUnorderedAsync(values, options, typeMap.GetDematerializer(options), this);
        }
    }
}

file static class Util
{
    internal static void WriteUnordered<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);

        using (builder.CreateParallelWriter(out var sink))
        {
            CsvParallel.ForEach<
                TValue,
                ParallelChunker.HasOrderEnumerable<TValue>,
                CsvWriterProducer<T, TValue>,
                CsvFieldWriter<T>
            >(
                ParallelChunker.Chunk(source, parallelOptions.EffectiveChunkSize),
                new CsvWriterProducer<T, TValue>(options, builder.IOOptions, dematerializer, sink),
                CsvWriterProducer<T>.Consume,
                cts,
                parallelOptions.MaxDegreeOfParallelism
            );
        }
    }

    internal static async Task WriteUnorderedAsync<T, TValue>(
        IAsyncEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvParallelOptions parallelOptions = builder.ParallelOptions;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parallelOptions.CancellationToken);
        await using (builder.CreateAsyncParallelWriter(out var sink))
        {
            await CsvParallel
                .ForEachAsync<
                    TValue,
                    ParallelChunker.HasOrderEnumerable<TValue>,
                    CsvWriterProducer<T, TValue>,
                    CsvFieldWriter<T>
                >(
                    ParallelChunker.ChunkAsync(source, parallelOptions.EffectiveChunkSize),
                    new CsvWriterProducer<T, TValue>(options, dematerializer, sink),
                    CsvWriterProducer<T>.ConsumeAsync,
                    cts,
                    parallelOptions.MaxDegreeOfParallelism
                )
                .ConfigureAwait(false);
        }
    }
}

file static class WriteImpl
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ForEach<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder,
        CancellationTokenSource cts
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CancellationToken cancellationToken = cts.Token;
        cancellationToken.ThrowIfCancellationRequested();

        using var _ = builder.CreateParallelWriter(out var sink);

        Exception? exception = null;

        if (options.HasHeader)
        {
            using var headerWriter = CreateWriter();
            dematerializer.WriteHeader(in headerWriter);
            headerWriter.WriteNewline();
            headerWriter.Writer.Complete(null);
        }

        int capacity = Environment.ProcessorCount;
        capacity = Math.Min(capacity, builder.ParallelOptions.MaxDegreeOfParallelism ?? capacity);
        BlockingCollection<CsvFieldWriter<T>> saturatedWriters = new(capacity);

        Task consumerTask = Task.Run(
            () =>
            {
                foreach (var writer in saturatedWriters.GetConsumingEnumerable(cancellationToken))
                {
                    try
                    {
                        if (exception is null)
                        {
                            writer.Writer.Flush();
                        }
                    }
                    catch (Exception e)
                    {
                        exception ??= e;
                        cts.Cancel();
                    }
                    finally
                    {
                        writer.Writer.Complete(exception);
                        writer.Dispose();
                    }
                }
            },
            CancellationToken.None
        );

        try
        {
            Parallel.ForEach(
                source: ParallelChunker.Chunk(source, builder.ParallelOptions.EffectiveChunkSize),
                parallelOptions: new ParallelOptions
                {
                    MaxDegreeOfParallelism = builder.ParallelOptions.MaxDegreeOfParallelism ?? -1,
                    CancellationToken = cancellationToken,
                },
                localInit: () => new StrongBox<CsvFieldWriter<T>>(CreateWriter()),
                body: (chunk, loopState, _, box) =>
                {
                    ref readonly CsvFieldWriter<T> writer = ref box.Value;

                    try
                    {
                        foreach (var obj in chunk)
                        {
                            if (writer.Writer.NeedsFlush)
                            {
                                saturatedWriters.Add(writer, cancellationToken);
                                box.Value = CreateWriter();
                                // TODO check if this is even necessary
                                writer = ref box.Value;
                            }

                            dematerializer.Write(in writer, obj);
                            writer.WriteNewline();
                        }
                    }
                    catch (Exception e)
                    {
                        exception = e;
                        loopState.Stop();
                        cts.Cancel();
                    }

                    return box;
                },
                localFinally: box => saturatedWriters.Add(box.Value, cancellationToken)
            );
        }
        catch (Exception e)
        {
            exception ??= e;
        }

        saturatedWriters.CompleteAdding();

        try
        {
            consumerTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            exception ??= e;
        }

        // ensure all buffers are returned even in case of exceptions
        while (saturatedWriters.TryTake(out var writer))
        {
            try
            {
                writer.Writer.Complete(exception);
            }
            catch { }
            finally
            {
                writer.Dispose();
            }
        }

        exception?.Rethrow();

        CsvFieldWriter<T> CreateWriter()
        {
            return new CsvFieldWriter<T>(new MemoryPoolBufferWriter<T>(sink, null, builder.IOOptions), options);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task ForEachAsync<T, TValue>(
        IEnumerable<TValue> source,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Csv.IParallelWriteBuilder<T> builder,
        CancellationTokenSource cts
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CancellationToken cancellationToken = cts.Token;
        cancellationToken.ThrowIfCancellationRequested();

        using var _ = builder.CreateParallelWriter(out var sink);

        Exception? exception = null;

        if (options.HasHeader)
        {
            using var headerWriter = CreateWriter();
            dematerializer.WriteHeader(in headerWriter);
            headerWriter.WriteNewline();
            headerWriter.Writer.Complete(null);
        }

        int capacity = Environment.ProcessorCount;
        capacity = Math.Min(capacity, builder.ParallelOptions.MaxDegreeOfParallelism ?? capacity);
        Channel<CsvFieldWriter<T>> channel = Channel.CreateBounded<CsvFieldWriter<T>>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );

        Task consumerTask = Task.Run(
            async () =>
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out CsvFieldWriter<T> writer))
                    {
                        try
                        {
                            if (exception is null)
                            {
                                await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (Exception e)
                        {
                            exception ??= e;
                            cts.Cancel();
                        }
                        finally
                        {
                            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
                            writer.Dispose();
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
                    source: ParallelChunker.Chunk(source, builder.ParallelOptions.EffectiveChunkSize),
                    parallelOptions: new ParallelOptions
                    {
                        MaxDegreeOfParallelism = builder.ParallelOptions.MaxDegreeOfParallelism ?? -1,
                        CancellationToken = cancellationToken,
                    },
                    body: async (chunk, innerToken) =>
                    {
                        CsvFieldWriter<T> writer = CreateWriter();

                        try
                        {
                            foreach (var obj in chunk)
                            {
                                if (writer.Writer.NeedsFlush)
                                {
                                    await channel.Writer.WriteAsync(writer, innerToken).ConfigureAwait(false);
                                    writer = CreateWriter();
                                }

                                dematerializer.Write(in writer, obj);
                                writer.WriteNewline();
                            }
                        }
                        catch (Exception e)
                        {
                            exception = e;
                            cts.Cancel();
                            writer.Dispose();
                        }

                        await channel.Writer.WriteAsync(writer, innerToken).ConfigureAwait(false);
                    }
                )
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            exception ??= e;
        }

        channel.Writer.Complete(exception);

        try
        {
            await consumerTask.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            exception ??= e;
        }

        // ensure all buffers are returned even in case of exceptions
        await foreach (var writer in channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            try
            {
                writer.Writer.Complete(exception);
            }
            catch { }
            finally
            {
                writer.Dispose();
            }
        }

        exception?.Rethrow();

        CsvFieldWriter<T> CreateWriter()
        {
            return new CsvFieldWriter<T>(new MemoryPoolBufferWriter<T>(sink, null, builder.IOOptions), options);
        }
    }
}
