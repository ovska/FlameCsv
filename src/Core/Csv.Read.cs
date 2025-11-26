using System.Buffers;
using System.Diagnostics;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

namespace FlameCsv;

static partial class Csv
{
    /// <summary>
    /// Base builder to create a synchronous CSV reading pipeline from.
    /// </summary>
    public interface IReadBuilderBase<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        /// <summary>
        /// Creates a CSV buffer reader from the builder.
        /// </summary>
        /// <param name="isAsync">
        /// Hint to the builder how the data will be read, for example to configure <see cref="FileOptions.Asynchronous"/>.
        /// The builder is free to ignore this parameter.
        /// </param>
        ICsvBufferReader<T> CreateReader(bool isAsync);

        /// <summary>
        /// Buffer pool to use for renting temporary buffers.<br/>
        /// If not set, <see cref="MemoryPool{T}.Shared"/> will be used.
        /// </summary>
        /// <seealso cref="IReadBuilder{T}.IOOptions"/>
        IBufferPool? BufferPool { get; }

        /// <summary>
        /// Reads CSV records as <typeparamref name="TValue"/>, binding them using reflection.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that parses <typeparamref name="TValue"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// The enumerator cannot be reused.<br/>
        /// The returned enumerable must be disposed after use, either explicitly or using <c>foreach</c>.
        /// </remarks>
        /// <seealso cref="IReadBuilderBase{T}.Read{TValue}(CsvTypeMap{T, TValue}, CsvOptions{T}?)"/>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        IEnumerable<TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(CsvOptions<T>? options = null)
        {
            return new CsvValueEnumerable<T, TValue>(this, options ?? CsvOptions<T>.Default);
        }

        /// <summary>
        /// Reads CSV records as <typeparamref name="TValue"/>, binding them using the given type map.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that parses <typeparamref name="TValue"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// The returned enumerable must be disposed after use, either explicitly or using <c>foreach</c>.
        /// </remarks>
        /// <seealso cref="IReadBuilderBase{T}.Read{TValue}(CsvOptions{T}?)"/>
        IEnumerable<TValue> Read<TValue>(CsvTypeMap<T, TValue> typeMap, CsvOptions<T>? options = null)
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            return new CsvTypeMapEnumerable<T, TValue>(this, options ?? CsvOptions<T>.Default, typeMap);
        }

        /// <summary>
        /// Enumerates CSV records from the underlying data.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that yields <see cref="CsvRecord{T}"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// <see cref="CsvRecord{T}"/> instances are only valid until <c>MoveNext()</c> or <c>Dispose()</c> is called on the enumerator.
        /// The returned enumerator is intended to be only used in a <c>foreach</c> loop, not in LINQ.
        /// </remarks>
        CsvRecordEnumerable<T> Enumerate(CsvOptions<T>? options = null)
        {
            return new CsvRecordEnumerable<T>(this, options ?? CsvOptions<T>.Default);
        }
    }

    /// <summary>
    /// Builder to create a CSV reading pipeline from.
    /// </summary>
    public interface IReadBuilder<T> : IReadBuilderBase<T>
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
        IReadBuilder<T> WithIOOptions(in CsvIOOptions ioOptions);

        IBufferPool? IReadBuilderBase<T>.BufferPool => IOOptions.BufferPool;

        /// <summary>
        /// Reads CSV records as <typeparamref name="TValue"/>, binding them using reflection.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that parses <typeparamref name="TValue"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// The enumerator cannot be reused.<br/>
        /// The returned enumerable must be disposed after use, either explicitly or using <c>foreach</c>.
        /// </remarks>
        [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
        IAsyncEnumerable<TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(CsvOptions<T>? options = null)
        {
            return new CsvValueEnumerable<T, TValue>(this, options ?? CsvOptions<T>.Default);
        }

        /// <summary>
        /// Reads CSV records as <typeparamref name="TValue"/>, binding them using the given type map.
        /// </summary>
        /// <param name="typeMap">Type map used to bind the CSV data</param>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that parses <typeparamref name="TValue"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// The enumerator cannot be reused.<br/>
        /// The returned enumerable must be disposed after use, either explicitly or using <c>foreach</c>.
        /// </remarks>
        IAsyncEnumerable<TValue> ReadAsync<TValue>(CsvTypeMap<T, TValue> typeMap, CsvOptions<T>? options = null)
        {
            ArgumentNullException.ThrowIfNull(typeMap);
            return new CsvTypeMapEnumerable<T, TValue>(this, options ?? CsvOptions<T>.Default, typeMap);
        }

        /// <summary>
        /// Enumerates CSV records asynchronously from the underlying data.
        /// </summary>
        /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
        /// <returns>
        /// An enumerable that yields <see cref="CsvRecord{T}"/> instances from the CSV data in a forward-only fashion.
        /// </returns>
        /// <remarks>
        /// <see cref="CsvRecord{T}"/> instances are only valid until <c>MoveNextAsync()</c> or <c>DisposeAsync()</c> is called on the enumerator.
        /// The returned enumerator is intended to be only used in an <c>await foreach</c> loop, not in LINQ.
        /// </remarks>
        CsvRecordAsyncEnumerable<T> EnumerateAsync(CsvOptions<T>? options = null)
        {
            return new CsvRecordAsyncEnumerable<T>(this, options ?? CsvOptions<T>.Default);
        }
    }

    /// <summary>
    /// Builder to create a CSV reading pipeline from a stream of bytes.
    /// </summary>
    public interface IReadStreamBuilder : IReadBuilder<byte>
    {
        /// <summary>
        /// Specifies the encoding of the underlying data and wraps the data source
        /// in a <see cref="TextReader"/>.
        /// </summary>
        /// <param name="encoding">Encoding to use</param>
        /// <returns>
        /// A builder to create a CSV reading pipeline from.
        /// </returns>
        IReadBuilder<char> WithEncoding(Encoding encoding);

        /// <summary>
        /// Specifies that the underlying data is encoded as UTF8 and wraps the data source
        /// in a <see cref="TextReader"/>.
        /// </summary>
        /// <returns>
        /// A builder to create a CSV reading pipeline from.
        /// </returns>
        IReadBuilder<char> WithUtf8Encoding() => WithEncoding(Encoding.UTF8);
    }

    private sealed class ReadMemoryBuilder<T>(ReadOnlyMemory<T> memory) : IReadBuilderBase<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public IBufferPool? BufferPool => null;

        ICsvBufferReader<T> IReadBuilderBase<T>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(memory);
        }
    }

    private class ReadSequenceBuilder<T>(in ReadOnlySequence<T> sequence, in CsvIOOptions ioOptions) : IReadBuilder<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        private readonly ReadOnlySequence<T> _sequence = sequence;
        private readonly CsvIOOptions _ioOptions = ioOptions;

        public CsvIOOptions IOOptions => _ioOptions;

        public IReadBuilder<T> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadSequenceBuilder<T>(in _sequence, in ioOptions);
        }

        public ICsvBufferReader<T> CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(in _sequence, in _ioOptions);
        }
    }

    private sealed class ReadTextBuilder : IReadBuilder<char>
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly TextReader? _reader;
        private readonly Stream? _stream;
        private readonly Encoding? _encoding;
        private readonly CsvIOOptions _ioOptions;

        internal ReadTextBuilder(TextReader reader, in CsvIOOptions ioOptions)
        {
            ArgumentNullException.ThrowIfNull(reader);

            _reader = reader;
            _ioOptions = ioOptions;
        }

        internal ReadTextBuilder(Stream stream, Encoding? encoding, in CsvIOOptions ioOptions)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Throw.IfNotReadable(stream);

            _stream = stream;
            _encoding = encoding;
            _ioOptions = ioOptions;
        }

        internal ReadTextBuilder(TextReader? reader, Stream? stream, Encoding? encoding, in CsvIOOptions ioOptions)
        {
            _reader = reader;
            _stream = stream;
            _encoding = encoding;
            _ioOptions = ioOptions;
        }

        public IReadBuilder<char> WithIOOptions(in CsvIOOptions ioOptions) =>
            new ReadTextBuilder(_reader, _stream, _encoding, in ioOptions);

        ICsvBufferReader<char> IReadBuilderBase<char>.CreateReader(bool isAsync)
        {
            if (_reader is null)
            {
                Debug.Assert(_stream is not null);
                return CsvBufferReader.Create(_stream, _encoding, in _ioOptions);
            }

            return CsvBufferReader.Create(_reader, in _ioOptions);
        }
    }

    private sealed class ReadStreamBuilder : IReadBuilder<char>, IReadStreamBuilder
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly Stream _stream;
        private readonly CsvIOOptions _ioOptions;

        internal ReadStreamBuilder(Stream stream, in CsvIOOptions ioOptions)
        {
            ArgumentNullException.ThrowIfNull(stream);
            Throw.IfNotReadable(stream);

            _stream = stream;
            _ioOptions = ioOptions;
        }

        IReadBuilder<char> IReadBuilder<char>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadStreamBuilder(_stream, in ioOptions);
        }

        IReadBuilder<byte> IReadBuilder<byte>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadStreamBuilder(_stream, in ioOptions);
        }

        ICsvBufferReader<char> IReadBuilderBase<char>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(_stream, null, in _ioOptions);
        }

        ICsvBufferReader<byte> IReadBuilderBase<byte>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(_stream, in _ioOptions);
        }

        IReadBuilder<char> IReadStreamBuilder.WithEncoding(Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
            return new ReadTextBuilder(null, _stream, encoding, in _ioOptions);
        }
    }

    private sealed class FileReaderBuilder : IReadBuilder<char>, IReadStreamBuilder
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly string _path;
        private readonly CsvIOOptions _ioOptions;
        private readonly Encoding? _encoding;

        public FileReaderBuilder(string path, in CsvIOOptions ioOptions, Encoding? encoding = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            _path = path;
            _ioOptions = ioOptions.ForFileIO();
            _encoding = encoding;
        }

        public IReadBuilder<char> WithEncoding(Encoding encoding) => new FileReaderBuilder(_path, _ioOptions, encoding);

        ICsvBufferReader<char> IReadBuilderBase<char>.CreateReader(bool isAsync)
        {
            FileStream stream = GetFileStream(isAsync);

            try
            {
                if (_encoding?.Equals(Encoding.UTF8) != false)
                {
                    return new Utf8StreamReader(stream, in _ioOptions);
                }

                return CsvBufferReader.Create(
                    new StreamReader(
                        stream,
                        _encoding,
                        detectEncodingFromByteOrderMarks: true,
                        _ioOptions.BufferSize,
                        leaveOpen: false
                    ),
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

        ICsvBufferReader<byte> IReadBuilderBase<byte>.CreateReader(bool isAsync)
        {
            Debug.Assert(_encoding is null);
            return new StreamBufferReader(GetFileStream(isAsync), in _ioOptions);
        }

        public IReadBuilder<char> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new FileReaderBuilder(_path, in ioOptions, _encoding);
        }

        IReadBuilder<byte> IReadBuilder<byte>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new FileReaderBuilder(_path, in ioOptions, _encoding);
        }

        private FileStream GetFileStream(bool isAsync)
        {
            return new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                _ioOptions.BufferSize,
                FileOptions.SequentialScan | (isAsync ? FileOptions.Asynchronous : FileOptions.None)
            );
        }
    }
}
