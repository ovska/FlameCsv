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
public sealed partial class CsvReader<T> : RecordOwner<T>, IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// If available, SIMD tokenizer than can be used to parse the CSV data.
    /// </summary>
    private readonly CsvTokenizer<T>? _tokenizer;

    /// <summary>
    /// Scalar tokenizer that is used to parse the tail end of the data, or as a fallback if SIMD is not available.
    /// </summary>
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    /// <summary>
    /// Whether the instance has been disposed.
    /// </summary>
    private bool IsDisposed => _state == State.Disposed;

    internal readonly RecordBuffer _recordBuffer;

    internal readonly Allocator<T> _unescapeAllocator;
    private EnumeratorStack _stackMemory; // don't make me readonly!

    internal readonly ICsvBufferReader<T> _reader;
    private ReadOnlyMemory<T> _buffer;

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
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        _recordBuffer = new RecordBuffer();
        _reader = reader;
        _skipBOM = typeof(T) == typeof(byte);
        _state = State.Initialized;

        _tokenizer = CsvTokenizer.Create(options);
        _scalarTokenizer = CsvTokenizer.CreateScalar(options);

        _unescapeAllocator = new MemoryPoolAllocator<T>(options.Allocator);
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
        if (_recordBuffer.TryPop(out RecordView record))
        {
            // we have a record in the buffer, return it
            slice = new CsvSlice<T>
            {
                Reader = this,
                Data = _buffer,
                Record = record,
            };
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
                TryFillCore(out RecordView record)
                || (_state == State.ReaderCompleted && TryFillCore(out record, readToEnd: true))
            )
            {
                slice = new()
                {
                    Reader = this,
                    Data = _buffer,
                    Record = record,
                };
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

    private bool TryFillCore(out RecordView record, bool readToEnd = false)
    {
        ReadOnlySpan<T> data = _buffer.Span;

        Read:
        int fieldsRead;

        if (readToEnd || _tokenizer is null)
        {
            FieldBuffer destination = _recordBuffer.GetUnreadBuffer(minimumLength: 0, out int startIndex);
            fieldsRead = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd);
        }
        else
        {
            FieldBuffer destination = _recordBuffer.GetUnreadBuffer(
                _tokenizer.MinimumFieldBufferSize,
                out int startIndex
            );
            fieldsRead = _tokenizer.Tokenize(destination, startIndex, data);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

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
        int consumed = _recordBuffer.Reset();

        if (consumed > 0)
        {
            Debug.Assert(consumed <= (_buffer.Length + 1), $"Buffer len {_buffer.Length}, but consumed was {consumed}");
            consumed = Math.Min(consumed, _buffer.Length);
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
        (ReadOnlyMemory<T> buffer, bool isCompleted) = result;

        // Check if the buffer length exceeds the maximum allowed length
        // the field metadata cannot hold more than 30 bits of information, so split huge continous buffers
        // into smaller chunks
        if (buffer.Length > Field.MaxFieldEnd)
        {
            buffer = buffer.Slice(0, Field.MaxFieldEnd);
            isCompleted = false;
        }

        _buffer = buffer;
        _state = isCompleted ? State.ReaderCompleted : State.Reading;
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
            _recordBuffer.Dispose();
        }
    }

    internal override Span<T> GetUnescapeBuffer(int length)
    {
        int stackLength = EnumeratorStack.Length / Unsafe.SizeOf<T>();

        // allocate a new buffer if the requested length is larger than the stack buffer
        if (length > stackLength)
        {
            return _unescapeAllocator.GetSpan(length);
        }

        return MemoryMarshal.Cast<byte, T>((Span<byte>)_stackMemory);
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
