using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
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
public sealed partial class CsvReader<T> : RecordOwner<T>, IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// If available, SIMD tokenizer than can be used to parse the CSV data.
    /// </summary>
    private CsvTokenizer<T>? _tokenizer;

    /// <summary>
    /// Scalar tokenizer that is used to parse the tail end of the data, or as a fallback if SIMD is not available.
    /// </summary>
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    internal readonly Allocator<T> _unescapeAllocator;
    private EnumeratorStack _stackMemory; // don't make me readonly!

    internal readonly ICsvBufferReader<T> _reader;
    internal ReadOnlyMemory<T> _buffer;

    /// <summary>
    /// UTF-8 BOM state.
    /// </summary>
    private Preamble _preamble;

    /// <summary>
    /// Current state of the parser.
    /// </summary>
    private State _state;

    /// <inheritdoc/>
    public override bool IsDisposed => _state >= State.Disposed;

    /// <inheritdoc cref="CsvReader{T}(CsvOptions{T},ICsvBufferReader{T},in CsvIOOptions)"/>
    public CsvReader(CsvOptions<T> options, ReadOnlyMemory<T> csv, in CsvIOOptions ioOptions = default)
        : this(options, CsvBufferReader.Create(csv), in ioOptions) { }

    /// <inheritdoc cref="CsvReader{T}(CsvOptions{T},ICsvBufferReader{T},in CsvIOOptions)"/>
    public CsvReader(CsvOptions<T> options, in ReadOnlySequence<T> csv, in CsvIOOptions ioOptions = default)
        : this(options, CsvBufferReader.Create(in csv), in ioOptions) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvReader{T}"/> class.
    /// </summary>
    public CsvReader(CsvOptions<T> options, ICsvBufferReader<T> reader, in CsvIOOptions ioOptions = default)
        : base(options, new RecordBuffer())
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        _reader = reader;
        _state = State.Initialized;

        (_scalarTokenizer, _tokenizer) = options.GetTokenizers();

        _unescapeAllocator = new Allocator<T>(ioOptions.EffectiveBufferPool);

        if (typeof(T) == typeof(byte))
        {
            // can be garbage on char as it's never read
            _preamble = Preamble.Unread;
        }
    }

    /// <summary>
    /// Attempts to read the next CSV record from the buffered data or read-ahead buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryReadLine(out RecordView record)
    {
        return _recordBuffer.TryPop(out record) || TryFillBuffer(out record);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryFillBuffer(out RecordView record)
    {
        ObjectDisposedException.ThrowIf(_state >= State.Disposed, this);

        ResetBufferAndAdvanceReader();

        // skip if the buffer is empty, the reader has completed, or we need to read more data
        if (_state is State.Reading or State.ReaderCompleted && !_buffer.IsEmpty)
        {
            if (
                TryFillCore(out record) || (_state == State.ReaderCompleted && TryFillCore(out record, readToEnd: true))
            )
            {
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

        Unsafe.SkipInit(out record);
        return false;
    }

    private bool TryFillCore(out RecordView record, bool readToEnd = false)
    {
        ReadOnlySpan<T> data = _buffer.Span;

        int fieldsRead;

        Read:
        if (readToEnd || _tokenizer is null)
        {
            Span<uint> destination = _recordBuffer.GetUnreadBuffer(minimumLength: 0, out int startIndex);
            fieldsRead = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd);

            Check.Positive(fieldsRead, "Scalar tokenizer should never return negative field count");
        }
        else
        {
            Span<uint> destination = _recordBuffer.GetUnreadBuffer(
                _tokenizer.MaxFieldsPerIteration,
                out int startIndex
            );
            fieldsRead = _tokenizer.Tokenize(destination, startIndex, data);

            // fall back to scalar parser on broken data
            if (fieldsRead < 0)
            {
                _tokenizer = null;
                goto Read;
            }
        }

        if (fieldsRead != 0)
        {
            _recordBuffer.SetFieldsRead(fieldsRead);

            // request more data if the next tokenizing would not be large enough to be productive
            if (
                _state == State.Reading
                && (data.Length - _recordBuffer.BufferedDataLength) < _tokenizer?.PreferredLength
            )
            {
                _state = State.DataExhausted;
            }

            if (_recordBuffer.TryPop(out record))
            {
                return true;
            }

            // read something, but no fully formed record.
            // ensure we aren't dealing with a huge record that can't fit in our buffer (thousands of fields)
            if (_recordBuffer.EnsureCapacity())
            {
                goto Read;
            }
        }

        Unsafe.SkipInit(out record);
        return false;
    }

    /// <summary>
    /// Attempts to reset the reader to the beginning of the data source.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the inner data source supports rewinding and was successfully reset;
    /// otherwise <c>false</c>.
    /// </returns>
    public bool TryReset()
    {
        ObjectDisposedException.ThrowIf(_state >= State.Disposed, this);

        if (_reader.TryReset())
        {
            SetReadResult(in CsvReadResult<T>.Empty);
            _recordBuffer.Initialize();
            _state = State.Initialized;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the reader to the beginning of the data source.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The inner data source does not support rewinding.
    /// </exception>
    public void Reset()
    {
        if (!TryReset())
        {
            throw new NotSupportedException("The inner data source does not support rewinding");
        }
    }

    /// <summary>
    /// Attempt to read more data from the underlying data source.
    /// </summary>
    /// <returns>
    /// <c>true</c> if more data was read; otherwise <c>false</c>.
    /// </returns>
    /// <remarks>
    /// All further reads after returning <c>false</c> will also return <c>false</c>.
    /// </remarks>
    internal bool TryAdvanceReader()
    {
        ObjectDisposedException.ThrowIf(_state >= State.Disposed, this);

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
    internal async ValueTask<bool> TryAdvanceReaderAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_state >= State.Disposed, this);
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
        const int maxLengthThreshold = Field.MaxFieldEnd - 1024;

        int consumed = _recordBuffer.Reset();

        if (consumed > 0)
        {
            Check.LessThanOrEqual(consumed, _buffer.Length + 1);

            consumed = Math.Min(consumed, _buffer.Length);

            if (typeof(T) == typeof(byte))
            {
                _reader.Advance(consumed + ((int)_preamble & 0b11));
                _preamble = 0;
            }
            else
            {
                _reader.Advance(consumed);
            }
            _buffer = _buffer.Slice(consumed);
        }
        else if (_state is State.DataExhausted && _buffer.Length >= maxLengthThreshold)
        {
            Throw.TooLongRecord(_buffer.Length);
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

        // Check if the buffer length exceeds the maximum allowed length. as the field metadata cannot
        // hold more than 30 bits of information, split huge continous buffers into smaller chunks
        if (_buffer.Length > Field.MaxFieldEnd)
        {
            _buffer = _buffer.Slice(0, Field.MaxFieldEnd);
            _state = State.Reading;
        }

        if (typeof(T) == typeof(byte) && _preamble != 0)
        {
            Check.Equal(_preamble, Preamble.Unread);
            TrySkipBOM();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TrySkipBOM()
    {
        if (typeof(T) != typeof(byte))
        {
            throw new UnreachableException();
        }

        if (_buffer.Span is [(byte)0xEF, (byte)0xBB, (byte)0xBF, ..])
        {
            _buffer = _buffer.Slice(3);
            _recordBuffer.BumpPreamble(3);
            _preamble = Preamble.NeedsSkip;
        }
        else
        {
            _preamble = Preamble.None;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_state >= State.Disposed)
            return;

        _state = State.Disposed;

        _reader.Dispose();
        _unescapeAllocator.Dispose();
        _recordBuffer.Dispose();
        _buffer = ReadOnlyMemory<T>.Empty; // don't hold on to data after disposing
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_state >= State.Disposed)
            return;

        _state = State.Disposed;

        await _reader.DisposeAsync().ConfigureAwait(false);
        _unescapeAllocator.Dispose();
        _recordBuffer.Dispose();
        _buffer = ReadOnlyMemory<T>.Empty; // don't hold on to data after disposing
    }

    internal override Span<T> GetUnescapeBuffer(int length)
    {
        int stackLength = EnumeratorStack.Length / Unsafe.SizeOf<T>();

        // allocate a new buffer if the requested length is larger than the stack buffer
        if (length > stackLength)
        {
            return _unescapeAllocator.GetSpan(length);
        }

        return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref _stackMemory.elem0), stackLength);
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

    private enum Preamble : byte
    {
        /// <summary>Preamble handled or does not need handling.</summary>
        None = 0,

        /// <summary>Preamble needs to be checked on the first read.</summary>
        Unread = 0x80,

        /// <summary>Preamble needs to be skipped on next advance.</summary>
        NeedsSkip = 0b11,
    }
}
