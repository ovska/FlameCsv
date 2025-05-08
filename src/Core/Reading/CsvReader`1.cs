using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Reads CSV records from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>
/// Internal implementation detail, this type should probably not be used directly.
/// </remarks>
[MustDisposeResource]
[PublicAPI]
[SkipLocalsInit]
public sealed partial class CsvReader<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options { get; }

    /// <summary>
    /// If available, SIMD tokenizer than can be used to parse the CSV data.
    /// </summary>
    private readonly CsvPartialTokenizer<T>? _simdTokenizer;

    /// <summary>
    /// Scalar tokenizer that is used to parse the tail end of the data, or as a fallback if SIMD is not available.
    /// </summary>
    private readonly CsvTokenizer<T> _scalarTokenizer;

    internal readonly Allocator<T> _unescapeAllocator;
    internal readonly Dialect<T> _dialect;

    /// <summary>
    /// Whether the instance has been disposed.
    /// </summary>
    private bool IsDisposed => _state == State.Disposed;

    private readonly MetaBuffer _metaBuffer;

    private readonly ICsvBufferReader<T> _reader;
    private ReadOnlyMemory<T> _buffer;

    private EnumeratorStack _stackMemory; // don't make me readonly

    /// <summary>
    /// Whether the UTF-8 BOM should be skipped on the next (first) read.
    /// </summary>
    private bool _skipBOM;

    /// <summary>
    /// Current state of the parser.
    /// </summary>
    private State _state;

    /// <inheritdoc cref="CsvReader{T}(CsvOptions{T},ICsvBufferReader{T})"/>
    public CsvReader(CsvOptions<T> options, ReadOnlyMemory<T> csv)
        : this(options, CsvBufferReader.Create(csv)) { }

    /// <inheritdoc cref="CsvReader{T}(CsvOptions{T},ICsvBufferReader{T})"/>
    public CsvReader(CsvOptions<T> options, in ReadOnlySequence<T> csv)
        : this(options, CsvBufferReader.Create(in csv)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReader{T}"/> class.
    /// </summary>
    public CsvReader(CsvOptions<T> options, ICsvBufferReader<T> reader)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        options.MakeReadOnly();

        Options = options;
        _metaBuffer = new MetaBuffer();
        _reader = reader;
        _skipBOM = typeof(T) == typeof(byte);
        _state = State.Initialized;

        _dialect = new Dialect<T>(options);
        _unescapeAllocator = new MemoryPoolAllocator<T>(options.Allocator);

        _simdTokenizer = CsvTokenizer.CreateSimd(options);
        _scalarTokenizer = CsvTokenizer.Create(options);

        Unsafe.SkipInit(out _stackMemory);
    }

    /// <summary>
    /// Attempts to return a complete CSV record from the read-ahead buffer.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a record was read,
    /// <c>false</c> if the read-ahead buffer is empty or the record is incomplete.
    /// </returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetBuffered(out CsvSlice<T> slice)
    {
        if (_metaBuffer.TryPop(out ArraySegment<Meta> meta))
        {
            slice = new() { Reader = this, Data = _buffer, Fields = meta };
            return true;
        }

        Unsafe.SkipInit(out slice);
        return false;
    }

    /// <summary>
    /// Attempts to read the next CSV record from the buffered data or read-ahead buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryReadLine(out CsvSlice<T> slice)
    {
        return TryGetBuffered(out slice) || TryFillBuffer(out slice);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryFillBuffer(out CsvSlice<T> slice)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        ResetBufferAndAdvanceReader();

        // skip if the buffer is empty, the reader has completed, or we need to read more data
        if (_state is State.Reading or State.ReaderCompleted && !_buffer.IsEmpty)
        {
            if (
                TryFillCore(out ArraySegment<Meta> meta)
                || (_state == State.ReaderCompleted && TryFillCore(out meta, readToEnd: true))
            )
            {
                slice = new() { Reader = this, Data = _buffer, Fields = meta };
                return true;
            }

            if (_state is State.Reading)
            {
                // could not read anything from the current buffer
                _state = State.DataExhausted;
            }
            else if (_state == State.ReaderCompleted)
            {
                // reader completed, but no more data to read
                _state = State.ReadToEnd;
            }
        }

        Unsafe.SkipInit(out slice);
        return false;
    }

    private bool TryFillCore(out ArraySegment<Meta> meta, bool readToEnd = false)
    {
        Span<Meta> metaBuffer = _metaBuffer.GetUnreadBuffer(out int startIndex);
        ReadOnlySpan<T> data = _buffer.Span;

        int read =
            readToEnd || _simdTokenizer is null
                ? _scalarTokenizer.Tokenize(metaBuffer, data, startIndex, readToEnd)
                : _simdTokenizer.Tokenize(metaBuffer, data, startIndex);

        if (read > 0)
        {
            int charactersConsumed = _metaBuffer.SetFieldsRead(read);

            // request more data if the next tokenizing would not be large enough to be productive
            if (_state == State.Reading && (data.Length - charactersConsumed) < _simdTokenizer?.PreferredLength)
            {
                _state = State.DataExhausted;
            }

            if (_metaBuffer.TryPop(out meta))
            {
                return true;
            }

            // read something, but no fully formed record.
            // ensure we aren't dealing with a huge record that can't fit in our buffer (thousands of fields)
            _metaBuffer.EnsureCapacity();
        }

        Unsafe.SkipInit(out meta);
        return false;
    }

    /// <summary>
    /// Attempts to reset the parser to the beginning of the data source.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the inner data source supports resetion and was successfully reset;
    /// otherwise <c>false</c>.
    /// </returns>
    public bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_reader.TryReset())
        {
            SetReadResult(in CsvReadResult<T>.Empty);
            _metaBuffer.Initialize();
            _state = State.Initialized;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempt to read more data from the underlying data.
    /// </summary>
    /// <returns>
    /// <c>true</c> if more data was read; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// All further reads after returning <c>false</c> will also return <c>false</c>.
    /// </remarks>
    internal bool TryAdvanceReader()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_state < State.ReaderCompleted)
        {
            ResetBufferAndAdvanceReader();
            CsvReadResult<T> result = _reader.Read();
            SetReadResult(in result);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="TryAdvanceReader"/>
    // TODO: profile pooling task builder
    internal async ValueTask<bool> TryAdvanceReaderAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_state < State.ReaderCompleted)
        {
            ResetBufferAndAdvanceReader();
            CsvReadResult<T> result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            SetReadResult(in result);
            return true;
        }

        return false;
    }

    private void ResetBufferAndAdvanceReader()
    {
        int consumed = _metaBuffer.Reset();

        if (consumed > 0)
        {
            _reader.Advance(consumed);
            _buffer = _buffer.Slice(consumed);
        }
    }

    /// <summary>
    /// Sets the result of a read operation.
    /// </summary>
    /// <param name="result">Result of the previous read to <see cref="ICsvBufferReader{T}"/></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetReadResult(ref readonly CsvReadResult<T> result)
    {
        _buffer = result.Buffer;
        _state = result.IsCompleted ? State.ReaderCompleted : State.Reading;
        if (typeof(T) == typeof(byte) && _skipBOM)
            TrySkipBOM();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TrySkipBOM()
    {
        Debug.Assert(typeof(T) == typeof(byte));

        if (_buffer.Span is [(byte)0xEF, (byte)0xBB, (byte)0xBF, ..])
        {
            _buffer = _buffer.Slice(3);
        }

        _skipBOM = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
            return;
        _state = State.Disposed;

        using (_reader)
        {
            DisposeCore();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
            return;
        _state = State.Disposed;

        await using (_reader.ConfigureAwait(false))
        {
            DisposeCore();
        }
    }

    private void DisposeCore()
    {
        using (_unescapeAllocator)
        {
            // don't hold on to data after disposing
            _buffer = ReadOnlyMemory<T>.Empty;
            _metaBuffer.Dispose();
        }
    }

    internal Span<T> GetUnescapeBuffer(int length)
    {
        if (typeof(T) == typeof(byte))
        {
            const int stackLength = EnumeratorStack.Length / sizeof(byte);

            // allocate a new buffer if the requested length is larger than the stack buffer
            if (length > stackLength)
            {
                return _unescapeAllocator.GetSpan(length);
            }

            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref _stackMemory.elem0), stackLength);
        }

        if (typeof(T) == typeof(char))
        {
            const int stackLength = EnumeratorStack.Length / sizeof(char);

            // allocate a new buffer if the requested length is larger than the stack buffer
            if (length > stackLength)
            {
                return _unescapeAllocator.GetSpan(length);
            }

            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref _stackMemory.elem0), stackLength);
        }

        throw Token<T>.NotSupported;
    }

    private enum State : byte
    {
        /// <summary>Instance created, but no data read yet.</summary>
        Initialized = 0,

        /// <summary>Reading has started.</summary>
        Reading = 1,

        /// <summary>The current data has been read to the field buffer, and new data is needed.</summary>
        DataExhausted = 2,

        /// <summary>Reader has completed, but the final records have not yet been parsed.</summary>
        ReaderCompleted = 3,

        /// <summary>The reader has completed, and all the data has been read into the buffer.</summary>
        ReadToEnd = 4,

        /// <summary>The parser has been disposed.</summary>
        Disposed = 5,
    }
}

[SkipLocalsInit]
[InlineArray(Length)]
internal struct EnumeratorStack
{
    public const int Length = 256;
    public byte elem0;
}
