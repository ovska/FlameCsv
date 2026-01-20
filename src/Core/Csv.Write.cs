using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

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
        /// The builder is free to ignore this parameter.
        /// </param>
        ICsvBufferWriter<T> CreateWriter(bool isAsync);

        /// <summary>
        /// Options to configure I/O for the CSV reader, such as buffer size and buffer pool.
        /// </summary>
        public CsvIOOptions IOOptions { get; }

        /// <summary>
        /// Configures the builder to use the given I/O options.
        /// </summary>
        /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
        TSelf WithIOOptions(in CsvIOOptions ioOptions);

        /// <summary>
        /// Creates a sink for parallel writing.
        /// </summary>
        /// <param name="flushAction">Action to use to write the data to the underlying destination</param>
        /// <returns>An instance that should be disposed once the writing has fully completed</returns>
        IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<T>> flushAction);

        /// <summary>
        /// Creates a sink for asynchronous parallel writing.
        /// </summary>
        /// <param name="flushAction"> Action to use to write the data to the underlying destination</param>
        /// <returns>An instance that should be disposed once the writing has fully completed</returns>
        IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> flushAction
        );

        /// <summary>
        /// Configures the builder to write CSV data in parallel.
        /// </summary>
        /// <param name="parallelOptions">Options to use for parallel writing</param>
        public IParallelWriteBuilder<T> AsParallel(CsvParallelOptions parallelOptions = default)
        {
            return new ParallelWriteWrapper<T, TSelf>(this, parallelOptions);
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

            if (_encoding is null || _encoding.CodePage == Encoding.UTF8.CodePage)
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

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>> flushAction)
        {
            return Util.ParallelFrom(CreateWriter(), out flushAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, CancellationToken, ValueTask> flushAction
        )
        {
            return Util.ParallelFrom(CreateWriter(), out flushAction);
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

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>> flushAction)
        {
            throw new NotSupportedException("Synchronous writing to PipeWriter is not supported.");
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> flushAction
        )
        {
            flushAction = async (data, ct) => await _pipeWriter.WriteAsync(data, ct).ConfigureAwait(false);
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

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>> flushAction)
        {
            return Util.ParallelFrom(CreateWriter(), out flushAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, CancellationToken, ValueTask> flushAction
        )
        {
            return Util.ParallelFrom(CreateWriter(), out flushAction);
        }

        private StreamWriter CreateWriter() => new(_stream, Encoding.UTF8, _ioOptions.BufferSize, _ioOptions.LeaveOpen);

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>> flushAction)
        {
            return Util.ParallelFrom(_stream, out flushAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> flushAction
        )
        {
            return Util.ParallelFrom(_stream, out flushAction);
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
                if (_encoding is null || _encoding.CodePage == Encoding.UTF8.CodePage)
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

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<char>> flushAction)
        {
            return Util.ParallelFrom(GetStreamWriter(isAsync: false), out flushAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<char>, CancellationToken, ValueTask> flushAction
        )
        {
            return Util.ParallelFrom(GetStreamWriter(isAsync: true), out flushAction);
        }

        public IDisposable? CreateParallelWriter(out Action<ReadOnlySpan<byte>> flushAction)
        {
            return Util.ParallelFrom(GetFileStream(isAsync: false), out flushAction);
        }

        public IAsyncDisposable? CreateAsyncParallelWriter(
            out Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> flushAction
        )
        {
            return Util.ParallelFrom(GetFileStream(isAsync: true), out flushAction);
        }
    }
}

file static class Util
{
    public static IDisposable? ParallelFrom(Stream stream, out Action<ReadOnlySpan<byte>> flushAction)
    {
        flushAction = span =>
        {
            stream.Write(span);
            stream.Flush();
        };

        return stream;
    }

    public static IAsyncDisposable? ParallelFrom(
        Stream stream,
        out Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> flushAction
    )
    {
        flushAction = async (data, ct) =>
        {
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        };

        return stream;
    }

    public static IDisposable? ParallelFrom(TextWriter writer, out Action<ReadOnlySpan<char>> flushAction)
    {
        flushAction = span =>
        {
            writer.Write(span);
            writer.Flush();
        };

        return writer;
    }

    public static IAsyncDisposable? ParallelFrom(
        TextWriter writer,
        out Func<ReadOnlyMemory<char>, CancellationToken, ValueTask> flushAction
    )
    {
        flushAction = async (data, ct) =>
        {
            await writer.WriteAsync(data, ct).ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);
        };

        return writer;
    }
}
