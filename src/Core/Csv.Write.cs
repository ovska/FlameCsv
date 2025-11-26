using System.Diagnostics;
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
    /// Base builder to create a synchronous CSV writing pipeline from.
    /// </summary>
    public interface IWriteBuilderBase<T>
        where T : unmanaged, IBinaryInteger<T>
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
    /// Base builder to create a CSV writing pipeline from.
    /// </summary>
    public interface IWriteBuilder<T> : IWriteBuilderBase<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        /// <summary>
        /// Options to configure I/O for the CSV reader, such as buffer size and buffer pool.
        /// </summary>
        public CsvIOOptions IOOptions { get; }

        /// <summary>
        /// Configures the builder to use the given I/O options.
        /// </summary>
        /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
        IWriteBuilder<T> WithIOOptions(in CsvIOOptions ioOptions);

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
    }

    /// <summary>
    /// Builder to create a CSV writing pipeline to a stream.
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

            Debug.Assert(_stream is not null);

            if (_encoding?.Equals(Encoding.UTF8) != false)
            {
                return new Utf8StreamWriter(_stream, in _ioOptions);
            }

            return new TextBufferWriter(
                new StreamWriter(_stream, _encoding, _ioOptions.BufferSize, _ioOptions.LeaveOpen),
                in _ioOptions
            );
        }

        IWriteBuilder<char> IWriteBuilder<char>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteTextBuilder(_writer, _stream, _encoding, in ioOptions);
        }
    }

    private sealed class WritePipeBuilder : IWriteBuilderBase<byte>
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

        ICsvBufferWriter<char> IWriteBuilderBase<char>.CreateWriter(bool isAsync)
        {
            return new Utf8StreamWriter(_stream, in _ioOptions);
        }

        IWriteBuilder<char> IWriteBuilder<char>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new WriteStreamBuilder(_stream, in ioOptions);
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
            ArgumentNullException.ThrowIfNull(path);
            _path = path;
            _encoding = encoding;
            _ioOptions = ioOptions.ForFileIO();
        }

        public ICsvBufferWriter<char> CreateWriter(bool isAsync)
        {
            FileStream stream = GetFileStream(isAsync);

            try
            {
                if (_encoding?.Equals(Encoding.UTF8) != false)
                {
                    return new Utf8StreamWriter(stream, in _ioOptions);
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

        ICsvBufferWriter<byte> IWriteBuilderBase<byte>.CreateWriter(bool isAsync)
        {
            return new StreamBufferWriter(GetFileStream(isAsync), in _ioOptions);
        }

        IWriteBuilder<byte> IWriteBuilder<byte>.WithIOOptions(in CsvIOOptions ioOptions)
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
    }
}
