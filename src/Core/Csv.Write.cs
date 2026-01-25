using System.Buffers;
using System.ComponentModel;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;

namespace FlameCsv;

static partial class Csv
{
    /// <summary>
    /// Base builder to create a CSV writing pipeline from.
    /// </summary>
    public interface IWriteBuilderBase<T, TSelf>
        where T : unmanaged, IBinaryInteger<T>
        where TSelf : IWriteBuilderBase<T, TSelf>
    {
        /// <summary>
        /// Creates a CSV buffer writer from the builder.
        /// </summary>
        /// <param name="isAsync">
        /// Hint to the builder how the data will be written, for example to configure <see cref="FileOptions.Asynchronous"/>.
        /// The builder is free to ignore this parameter, and has no effect on whether the returned writer supports asynchronous operations.
        /// </param>
        ICsvBufferWriter<T> CreateWriter(bool isAsync);

        /// <summary>
        /// Options to configure I/O for the CSV reader, such as buffer size and memory pool.
        /// </summary>
        public CsvIOOptions IOOptions { get; }

        /// <summary>
        /// Configures the builder to use the given I/O options.
        /// </summary>
        /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
        TSelf WithIOOptions(in CsvIOOptions ioOptions);

        /// <summary>
        /// Creates a sink for parallel writing. The sink is not thread-safe.
        /// </summary>
        /// <param name="drainAction">
        /// Action to use to write the data to the underlying destination, with a boolean indicating whether this is the final flush.
        /// </param>
        /// <returns>An optional instance that should be disposed once the writing has fully completed</returns>
        IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<T>, bool> drainAction);

        /// <summary>
        /// Creates a sink for asynchronous parallel writing. The sink is not thread-safe.
        /// </summary>
        /// <param name="drainAction">
        /// Action to use to write the data to the underlying destination, with a boolean indicating whether this is the final flush.
        /// </param>
        /// <returns>An optional instance that should be disposed once the writing has fully completed</returns>
        IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask> drainAction
        );

        /// <summary>
        /// Configures the builder to write CSV data in parallel.
        /// </summary>
        /// <param name="parallelOptions">
        /// Options to use for parallel writing. You can pass a cancellation token to implicitly convert it to <see cref="CsvParallelOptions"/>.
        /// </param>
        public IParallelWriteBuilder<T> AsParallel(CsvParallelOptions parallelOptions = default)
        {
            return new ParallelWriteWrapper<T, TSelf>(this, parallelOptions);
        }

        /// <summary>
        /// Returns a writer instance that can be used to write custom fields, multiple different types,
        /// or multiple CSV documents into the same output.<br/>
        /// After use, the writer should be disposed, or completed with <see cref="CsvWriter{T}.Complete"/> or
        /// <see cref="CsvWriter{T}.CompleteAsync"/>.
        /// </summary>
        /// <param name="options">Options instance. If null, <see cref="CsvOptions{T}.Default"/> is used</param>
        /// <returns>Writer instance</returns>
        public CsvWriter<T> ToWriter(CsvOptions<T>? options = null)
        {
            return new CsvWriter<T>(
                new CsvFieldWriter<T>(CreateWriter(isAsync: false), options ?? CsvOptions<T>.Default)
            );
        }

        /// <summary>
        /// Returns a writer instance that can be used to write custom fields, multiple different types,
        /// or multiple CSV documents into the same output.<br/>
        /// After use, the writer should be disposed, or completed with <see cref="CsvWriter{T}.Complete"/> or
        /// <see cref="CsvWriter{T}.CompleteAsync"/>.
        /// </summary>
        /// <param name="options">Options instance. If null, <see cref="CsvOptions{T}.Default"/> is used</param>
        /// <returns>Writer instance</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public CsvFieldWriter<T> ToFieldWriter(CsvOptions<T>? options = null)
        {
            return new CsvFieldWriter<T>(CreateWriter(isAsync: false), options ?? CsvOptions<T>.Default);
        }
    }

    /// <summary>
    /// Builder to create a CSV writing pipeline from.
    /// </summary>
    public interface IWriteBuilder<T> : IWriteBuilderBase<T, IWriteBuilder<T>>
        where T : unmanaged, IBinaryInteger<T>
    {
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

            CsvWriter.WriteCore(values, this, options, options.TypeBinder.GetDematerializer<TValue>());
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

            CsvWriter.WriteCore(values, this, options, typeMap.GetDematerializer(options));
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
        /// </summary>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous writing operation</param>
        /// <returns>A task that completes when writing has finished</returns>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            IEnumerable<TValue> values,
            CsvOptions<T>? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;

            return CsvWriter.WriteAsyncCore(
                values,
                this,
                options,
                options.TypeBinder.GetDematerializer<TValue>(),
                cancellationToken
            );
        }

        /// <inheritdoc cref="WriteAsync{TValue}(IEnumerable{TValue}, CsvOptions{T}?, CancellationToken)"/>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            IAsyncEnumerable<TValue> values,
            CsvOptions<T>? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;

            return CsvWriter.WriteAsyncCore(
                values,
                this,
                options,
                options.TypeBinder.GetDematerializer<TValue>(),
                cancellationToken
            );
        }

        /// <summary>
        /// Writes CSV records to the target, binding them using reflection.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="values">Values to write</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <param name="cancellationToken">Token to cancel the asynchronous writing operation</param>
        /// <returns>A task that completes when writing has finished</returns>
        /// <remarks>
        /// A header or newline (depending on configuration) is written even if <paramref name="values"/> is empty.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        public Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
            CsvTypeMap<T, TValue> typeMap,
            IEnumerable<TValue> values,
            CsvOptions<T>? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;

            return CsvWriter.WriteAsyncCore(
                values,
                this,
                options,
                options.TypeBinder.GetDematerializer<TValue>(),
                cancellationToken
            );
        }

        /// <inheritdoc cref="WriteAsync{TValue}(IEnumerable{TValue}, CsvOptions{T}?, CancellationToken)"/>
        public Task WriteAsync<TValue>(
            CsvTypeMap<T, TValue> typeMap,
            IAsyncEnumerable<TValue> values,
            CsvOptions<T>? options = null,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            ArgumentNullException.ThrowIfNull(values);
            options ??= CsvOptions<T>.Default;

            return CsvWriter.WriteAsyncCore(
                values,
                this,
                options,
                typeMap.GetDematerializer(options),
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Builder to create a CSV writing pipeline to a stream.<br/>
    /// You can either write raw UTF8 directly as <c>byte</c>, or specify an encoding to use <c>char</c>.
    /// </summary>
    public interface IWriteStreamBuilder : IWriteBuilder<byte>
    {
        /// <summary>
        /// Specifies the encoding of the underlying data and wraps the destination
        /// in a <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="encoding">Encoding to use</param>
        /// <returns>
        /// A builder to create a CSV writing pipeline to.
        /// </returns>
        IWriteBuilder<char> WithEncoding(Encoding encoding);

        /// <summary>
        /// Specifies that the underlying data is encoded as UTF8 and wraps the destination
        /// in a <see cref="TextWriter"/>.
        /// </summary>
        /// <returns>
        /// A builder to create a CSV writing pipeline to.
        /// </returns>
        IWriteBuilder<char> WithUtf8Encoding() => WithEncoding(Encoding.UTF8);
    }

    private sealed class WriteTextBuilder : IWriteBuilder<char>
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly TextWriter? _writer;
        private readonly Stream? _stream;
        private readonly Encoding? _encoding;
        private readonly CsvIOOptions _ioOptions;

        public WriteTextBuilder(TextWriter writer, in CsvIOOptions ioOptions = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            _writer = writer;
            _ioOptions = ioOptions;
        }

        internal WriteTextBuilder(Stream stream, Encoding? encoding, in CsvIOOptions ioOptions)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Throw.IfNotWritable(stream);

            _stream = stream;
            _encoding = encoding;
            _ioOptions = ioOptions;
        }

        internal WriteTextBuilder(TextWriter? writer, Stream? stream, Encoding? encoding, in CsvIOOptions ioOptions)
        {
            _writer = writer;
            _stream = stream;
            _encoding = encoding;
            _ioOptions = ioOptions;
        }

        public ICsvBufferWriter<char> CreateWriter(bool isAsync)
        {
            if (_writer is not null)
            {
                return new TextBufferWriter(_writer, in _ioOptions);
            }

            Check.NotNull(_stream);

            if (!_ioOptions.DisableOptimizations && (_encoding is null || _encoding.CodePage == Encoding.UTF8.CodePage))
            {
                // Match StreamWriter behavior:
                // - Write preamble if encoding is explicit and has one
                // - Skip if seekable stream is not at position 0 (appending)
                bool writePreamble = _encoding?.Preamble.Length > 0 && (!_stream.CanSeek || _stream.Position == 0);
                return new Utf8StreamWriter(_stream, in _ioOptions, writePreamble);
            }

            return new TextBufferWriter(
                new StreamWriter(_stream, _encoding, _ioOptions.BufferSize, _ioOptions.LeaveOpen),
                in _ioOptions
            );
        }

        IWriteBuilder<char> IWriteBuilderBase<char, IWriteBuilder<char>>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteTextBuilder(_writer, _stream, _encoding, in ioOptions);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>, bool> drainAction)
        {
            return Util.ParallelFrom(CreateWriter(), out drainAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            return Util.ParallelFrom(CreateWriter(), out drainAction);
        }

        private TextWriter CreateWriter()
        {
            TextWriter? writer = _writer;

            if (writer is null)
            {
                Check.NotNull(_stream);
                writer = new StreamWriter(
                    _stream,
                    _encoding ?? Encoding.UTF8,
                    _ioOptions.BufferSize,
                    _ioOptions.LeaveOpen
                );
            }

            return writer;
        }
    }

    private sealed class WritePipeBuilder : IWriteBuilder<byte>
    {
        private readonly PipeWriter _pipeWriter;
        private readonly IBufferPool? _bufferPool;

        public WritePipeBuilder(PipeWriter pipeWriter, IBufferPool? bufferPool)
        {
            ArgumentNullException.ThrowIfNull(pipeWriter);
            _pipeWriter = pipeWriter;
            _bufferPool = bufferPool;
        }

        public ICsvBufferWriter<byte> CreateWriter(bool isAsync)
        {
            if (!isAsync)
            {
                Throw.NotSupported("Synchronous writing to PipeWriter is not supported.");
            }

            return new PipeBufferWriter(_pipeWriter, _bufferPool);
        }

        public CsvIOOptions IOOptions => new() { BufferPool = _bufferPool };

        public IWriteBuilder<byte> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WritePipeBuilder(_pipeWriter, ioOptions.BufferPool);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>, bool> drainAction)
        {
            throw new NotSupportedException("Synchronous writing to PipeWriter is not supported.");
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            drainAction = async (data, _, ct) => await _pipeWriter.WriteAsync(data, ct).ConfigureAwait(false);
            return null;
        }
    }

    private sealed class WriteStreamBuilder : IWriteBuilder<char>, IWriteStreamBuilder
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly Stream _stream;
        private readonly CsvIOOptions _ioOptions;

        internal WriteStreamBuilder(Stream stream, in CsvIOOptions ioOptions)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Throw.IfNotWritable(stream);

            _stream = stream;
            _ioOptions = ioOptions;
        }

        public ICsvBufferWriter<byte> CreateWriter(bool isAsync)
        {
            return new StreamBufferWriter(_stream, in _ioOptions);
        }

        public IWriteBuilder<char> WithEncoding(Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
            return new WriteTextBuilder(_stream, encoding, in _ioOptions);
        }

        public IWriteBuilder<byte> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteStreamBuilder(_stream, in ioOptions);
        }

        ICsvBufferWriter<char> IWriteBuilderBase<char, IWriteBuilder<char>>.CreateWriter(bool isAsync)
        {
            return new Utf8StreamWriter(_stream, in _ioOptions, writePreamble: false);
        }

        IWriteBuilder<char> IWriteBuilderBase<char, IWriteBuilder<char>>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteStreamBuilder(_stream, in ioOptions);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>, bool> drainAction)
        {
            return Util.ParallelFrom(CreateWriter(), out drainAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            return Util.ParallelFrom(CreateWriter(), out drainAction);
        }

        private StreamWriter CreateWriter() => new(_stream, Encoding.UTF8, _ioOptions.BufferSize, _ioOptions.LeaveOpen);

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>, bool> drainAction)
        {
            return Util.ParallelFrom(_stream, out drainAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            return Util.ParallelFrom(_stream, out drainAction);
        }
    }

    private sealed class WriteFileBuilder : IWriteBuilder<char>, IWriteStreamBuilder
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly string _path;
        private readonly CsvIOOptions _ioOptions;
        private readonly Encoding? _encoding;

        public WriteFileBuilder(string path, Encoding? encoding, in CsvIOOptions ioOptions = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            _path = path;
            _encoding = encoding;
            _ioOptions = ioOptions.ForFileIO();
        }

        public ICsvBufferWriter<char> CreateWriter(bool isAsync)
        {
            FileStream stream = GetFileStream(isAsync);

            try
            {
                if (
                    !_ioOptions.DisableOptimizations
                    && (_encoding is null || _encoding.CodePage == Encoding.UTF8.CodePage)
                )
                {
                    return new Utf8StreamWriter(stream, in _ioOptions, writePreamble: _encoding?.Preamble.Length > 0);
                }

                return new TextBufferWriter(
                    new StreamWriter(stream, _encoding, _ioOptions.BufferSize, leaveOpen: false),
                    in _ioOptions
                );
            }
            catch
            {
                // exception before we returned control to the caller
                stream.Dispose();
                throw;
            }
        }

        public IWriteBuilder<char> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteFileBuilder(_path, _encoding, in ioOptions);
        }

        public IWriteBuilder<char> WithEncoding(Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
            return new WriteFileBuilder(_path, encoding, in _ioOptions);
        }

        ICsvBufferWriter<byte> IWriteBuilderBase<byte, IWriteBuilder<byte>>.CreateWriter(bool isAsync)
        {
            return new StreamBufferWriter(GetFileStream(isAsync), in _ioOptions);
        }

        IWriteBuilder<byte> IWriteBuilderBase<byte, IWriteBuilder<byte>>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteFileBuilder(_path, _encoding, in ioOptions);
        }

        private FileStream GetFileStream(bool isAsync)
        {
            return new FileStream(
                _path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                _ioOptions.BufferSize,
                FileOptions.SequentialScan | (isAsync ? FileOptions.Asynchronous : FileOptions.None)
            );
        }

        private StreamWriter GetStreamWriter(bool isAsync)
        {
            return new StreamWriter(
                GetFileStream(isAsync),
                _encoding ?? Encoding.UTF8,
                _ioOptions.BufferSize,
                leaveOpen: false
            );
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>, bool> drainAction)
        {
            return Util.ParallelFrom(GetStreamWriter(isAsync: false), out drainAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            return Util.ParallelFrom(GetStreamWriter(isAsync: true), out drainAction);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>, bool> drainAction)
        {
            return Util.ParallelFrom(GetFileStream(isAsync: false), out drainAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            return Util.ParallelFrom(GetFileStream(isAsync: true), out drainAction);
        }
    }

    private sealed class WriteBWBuilder<T> : IWriteBuilder<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        private readonly IBufferWriter<T> _writer;

        public WriteBWBuilder(IBufferWriter<T> writer, in CsvIOOptions ioOptions = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            _writer = writer;
            IOOptions = ioOptions;
        }

        public CsvIOOptions IOOptions { get; }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask> drainAction
        )
        {
            drainAction = (data, _, ct) =>
            {
                if (ct.IsCancellationRequested)
                    return ValueTask.FromCanceled(ct);

                _writer.Write(data.Span);
                return ValueTask.CompletedTask;
            };
            return null;
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<T>, bool> drainAction)
        {
            drainAction = (span, _) => _writer.Write(span);
            return null;
        }

        public ICsvBufferWriter<T> CreateWriter(bool isAsync)
        {
            return new BufferWriterWrapper<T>(_writer, IOOptions.EffectiveBufferPool);
        }

        public IWriteBuilder<T> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteBWBuilder<T>(_writer, in ioOptions);
        }
    }
}

file static class Util
{
    public static IDisposable? ParallelFrom(Stream stream, out Action<ReadOnlySpan<byte>, bool> drainAction)
    {
        drainAction = (span, flush) =>
        {
            stream.Write(span);

            if (flush)
            {
                stream.Flush();
            }
        };

        return stream;
    }

    public static IAsyncDisposable? ParallelFrom(
        Stream stream,
        out Func<ReadOnlyMemory<byte>, bool, CancellationToken, ValueTask> drainAction
    )
    {
        drainAction = async (data, flush, ct) =>
        {
            await stream.WriteAsync(data, ct).ConfigureAwait(false);

            if (flush)
            {
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }
        };

        return stream;
    }

    public static IDisposable? ParallelFrom(TextWriter writer, out Action<ReadOnlySpan<char>, bool> drainAction)
    {
        drainAction = (span, flush) =>
        {
            writer.Write(span);

            if (flush)
            {
                writer.Flush();
            }
        };

        return writer;
    }

    public static IAsyncDisposable? ParallelFrom(
        TextWriter writer,
        out Func<ReadOnlyMemory<char>, bool, CancellationToken, ValueTask> drainAction
    )
    {
        drainAction = async (data, flush, ct) =>
        {
            await writer.WriteAsync(data, ct).ConfigureAwait(false);

            if (flush)
            {
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        };

        return writer;
    }
}
