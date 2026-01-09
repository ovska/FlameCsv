using System.Buffers;
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
    /// Base builder to create a CSV reading pipeline from.
    /// </summary>
    public interface IReadBuilderBase<T, TSelf>
        where T : unmanaged, IBinaryInteger<T>
        where TSelf : IReadBuilderBase<T, TSelf>
    {
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
        /// Creates a CSV buffer reader from the builder.
        /// </summary>
        /// <param name="isAsync">
        /// Hint to the builder how the data will be read, for example to configure <see cref="FileOptions.Asynchronous"/>.
        /// The builder is free to ignore this parameter.
        /// </param>
        ICsvBufferReader<T> CreateReader(bool isAsync);

        internal IParallelReader<T> CreateParallelReader(CsvOptions<T> options, bool isAsync);

        /// <summary>
        /// Configures the builder to read CSV data in parallel.
        /// </summary>
        /// <param name="parallelOptions">Options to use for parallel reading</param>
        public IParallelReadBuilder<T> AsParallel(CsvParallelOptions parallelOptions = default)
        {
            return new ReadParallelBuilder<T, TSelf>((TSelf)this, parallelOptions);
        }
    }

    /// <summary>
    /// Builder to create a CSV reading pipeline from.
    /// </summary>
    public interface IReadBuilder<T> : IReadBuilderBase<T, IReadBuilder<T>>
        where T : unmanaged, IBinaryInteger<T>
    {
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
        /// <seealso cref="IReadBuilder{T}.Read{TValue}(CsvTypeMap{T, TValue}, CsvOptions{T}?)"/>
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
        /// <seealso cref="IReadBuilder{T}.Read{TValue}(CsvOptions{T}?)"/>
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
    /// Builder to create a CSV reading pipeline from a stream of bytes.<br/>
    /// You can either read raw UTF8 directly as <c>byte</c>, or specify an encoding to use <c>char</c>.
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

    private sealed class ReadMemoryBuilder<T>(ReadOnlyMemory<T> memory, in CsvIOOptions ioOptions = default)
        : IReadBuilder<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public CsvIOOptions IOOptions => _ioOptions;

        private readonly ReadOnlyMemory<T> _memory = memory;
        private readonly CsvIOOptions _ioOptions = ioOptions;

        ICsvBufferReader<T> IReadBuilderBase<T, IReadBuilder<T>>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(_memory);
        }

        IParallelReader<T> IReadBuilderBase<T, IReadBuilder<T>>.CreateParallelReader(
            CsvOptions<T> options,
            bool isAsync
        )
        {
            return new ParallelSequenceReader<T>(new ReadOnlySequence<T>(_memory), options, in _ioOptions);
        }

        public IReadBuilder<T> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadMemoryBuilder<T>(_memory, in _ioOptions);
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

        IParallelReader<T> IReadBuilderBase<T, IReadBuilder<T>>.CreateParallelReader(
            CsvOptions<T> options,
            bool isAsync
        )
        {
            return new ParallelSequenceReader<T>(in _sequence, options, in _ioOptions);
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

        ICsvBufferReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateReader(bool isAsync)
        {
            if (_reader is null)
            {
                Check.NotNull(_stream);
                return CsvBufferReader.Create(_stream, _encoding, in _ioOptions);
            }

            return CsvBufferReader.Create(_reader, in _ioOptions);
        }

        IParallelReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateParallelReader(
            CsvOptions<char> options,
            bool isAsync
        )
        {
            TextReader? reader = _reader ?? Util.ToTextReader(_stream!, _encoding, in _ioOptions);
            return new ParallelTextReader(reader, options, _ioOptions);
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

        IReadBuilder<char> IReadBuilderBase<char, IReadBuilder<char>>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadStreamBuilder(_stream, in ioOptions);
        }

        IReadBuilder<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new ReadStreamBuilder(_stream, in ioOptions);
        }

        ICsvBufferReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(_stream, null, in _ioOptions);
        }

        ICsvBufferReader<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.CreateReader(bool isAsync)
        {
            return CsvBufferReader.Create(_stream, in _ioOptions);
        }

        IReadBuilder<char> IReadStreamBuilder.WithEncoding(Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
            return new ReadTextBuilder(null, _stream, encoding, in _ioOptions);
        }

        IParallelReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateParallelReader(
            CsvOptions<char> options,
            bool isAsync
        )
        {
            return new ParallelTextReader(Util.ToTextReader(_stream, null, in _ioOptions), options, _ioOptions);
        }

        IParallelReader<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.CreateParallelReader(
            CsvOptions<byte> options,
            bool isAsync
        )
        {
            return new ParallelStreamReader(_stream, options, _ioOptions);
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

        ICsvBufferReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateReader(bool isAsync)
        {
            Check.False(_ioOptions.LeaveOpen);

            FileStream stream = GetFileStream(isAsync);

            try
            {
                return CsvBufferReader.Create(stream, _encoding, in _ioOptions);
            }
            catch
            {
                // exception before we returned control to the caller
                stream.Dispose();
                throw;
            }
        }

        ICsvBufferReader<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.CreateReader(bool isAsync)
        {
            Check.IsNull(_encoding, "Bytes should be read from a file only without a specified encoding.");
            return new StreamBufferReader(GetFileStream(isAsync), in _ioOptions);
        }

        IParallelReader<char> IReadBuilderBase<char, IReadBuilder<char>>.CreateParallelReader(
            CsvOptions<char> options,
            bool isAsync
        )
        {
            return new ParallelTextReader(
                Util.ToTextReader(GetFileStream(isAsync), _encoding, in _ioOptions),
                options,
                _ioOptions
            );
        }

        IParallelReader<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.CreateParallelReader(
            CsvOptions<byte> options,
            bool isAsync
        )
        {
            Check.IsNull(_encoding, "Bytes should be read from a file only without a specified encoding.");
            return new ParallelStreamReader(GetFileStream(isAsync), options, _ioOptions);
        }

        public IReadBuilder<char> WithIOOptions(in CsvIOOptions ioOptions)
        {
            return new FileReaderBuilder(_path, in ioOptions, _encoding);
        }

        IReadBuilder<byte> IReadBuilderBase<byte, IReadBuilder<byte>>.WithIOOptions(in CsvIOOptions ioOptions)
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

file static class Util
{
    public static TextReader ToTextReader(Stream stream, Encoding? encoding, in CsvIOOptions ioOptions)
    {
        return new StreamReader(
            stream,
            encoding ?? Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: ioOptions.BufferSize,
            leaveOpen: ioOptions.LeaveOpen
        );
    }
}
