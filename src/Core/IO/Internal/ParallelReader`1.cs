using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal abstract class ParallelReader<T> : IParallelReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;

    private readonly IBufferPool _pool;
    private readonly int _bufferSize;

    private CsvTokenizer<T>? _tokenizer;
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    private bool _isCompleted;
    private bool _needToSkipPreamble;

    private ReadOnlyMemory<T> _previousRead;
    private IMemoryOwner<T>? _leftoverOwner;

    private int _recordBufferSize = 1024;

    private long _position;
    private int _lineNumber;
    private int _index;

    protected ParallelReader(CsvOptions<T> options, CsvIOOptions ioOptions)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        _options = options;

        _bufferSize = ioOptions.BufferSize;
        _pool = ioOptions.EffectiveBufferPool;

        _previousRead = ReadOnlyMemory<T>.Empty;
        _leftoverOwner = null;
        _position = 0;
        _lineNumber = 0;
        _index = 0;

        (_scalarTokenizer, _tokenizer) = options.GetTokenizers();

        if (typeof(T) == typeof(byte))
        {
            _needToSkipPreamble = true;
        }
    }

    protected abstract int ReadCore(Span<T> buffer);
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);
    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeAsyncCore();

    public Chunk<T>? Read()
    {
        ObjectDisposedException.ThrowIf(_position == -1, this);

        while (!_isCompleted)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);

            try
            {
                Memory<T> memory = owner.Memory;
                int read = ReadCore(memory.Slice(startIndex).Span);

                if (read == 0)
                {
                    owner.Dispose();
                    _isCompleted = true;
                    break;
                }

                if (TryReadCore(owner, memory.Slice(0, read + startIndex)) is { } chunk)
                {
                    return chunk;
                }
            }
            catch
            {
                // ownership not passed to caller yet
                owner.Dispose();
                throw;
            }
        }

        return TryReadLast();
    }

    public async ValueTask<Chunk<T>?> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_position == -1, this);

        while (!_isCompleted)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);

            try
            {
                Memory<T> memory = owner.Memory;
                int read = await ReadAsyncCore(memory.Slice(startIndex), cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    owner.Dispose();
                    _isCompleted = true;
                    break;
                }

                if (TryReadCore(owner, memory.Slice(0, read + startIndex)) is { } chunk)
                {
                    return chunk;
                }
            }
            catch
            {
                // ownership not passed to caller yet
                owner.Dispose();
                throw;
            }
        }

        return TryReadLast();
    }

    private Chunk<T>? TryReadLast()
    {
        _isCompleted = true;

        ReadOnlyMemory<T> lastBuffer = _previousRead;
        _previousRead = ReadOnlyMemory<T>.Empty;

        if (lastBuffer.IsEmpty)
        {
            return null;
        }

        Check.NotNull(_leftoverOwner);

        RecordBuffer recordBuffer = new(_recordBufferSize);
        Span<uint> destination = recordBuffer.GetUnreadBuffer(0, out int startIndex);
        int fieldsRead = _scalarTokenizer.Tokenize(destination, startIndex, lastBuffer.Span, readToEnd: true);
        int recordsRead = recordBuffer.SetFieldsRead(fieldsRead);
        Check.OverZero(recordsRead, "No complete records found in final data?");
        var chunk = new Chunk<T>(
            _index,
            _lineNumber,
            _position,
            _options,
            lastBuffer,
            _pool,
            _leftoverOwner,
            recordBuffer
        );
        _leftoverOwner = null;
        return chunk;
    }

    private Chunk<T>? TryReadCore(IMemoryOwner<T> owner, Memory<T> buffer)
    {
        if (typeof(T) == typeof(byte) && _needToSkipPreamble)
        {
            SkipPreamble(ref buffer);
        }

        RecordBuffer recordBuffer = new(_recordBufferSize);

        Span<uint> destination = recordBuffer.GetUnreadBuffer(
            _tokenizer?.MaxFieldsPerIteration ?? 0,
            out int startIndex
        );

        ReadOnlySpan<T> data = buffer.Span;

        Read:
        int fieldsRead = _tokenizer is null
            ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: false)
            : _tokenizer.Tokenize(destination, startIndex, data);

        if (fieldsRead < 0)
        {
            // fall back to scalar parser for pathological data
            Check.NotNull(_tokenizer);
            _tokenizer = null;
            goto Read;
        }

        int recordsRead = recordBuffer.SetFieldsRead(fieldsRead);

        Chunk<T>? chunk = null;
        int consumed = 0;

        if (recordsRead > 0)
        {
            // Adjust the next buffer's size based on utilization

            // If we're using more than 75% of the buffer capacity, grow it
            if (fieldsRead > (_recordBufferSize * 3 / 4))
            {
                _recordBufferSize = Math.Min(_recordBufferSize * 2, 4096 * 16);
            }
            // If we're using less than 25% of the buffer capacity, shrink it
            else if (fieldsRead < (_recordBufferSize / 4))
            {
                _recordBufferSize = Math.Max(_recordBufferSize / 2, 256);
            }

            consumed = recordBuffer.BufferedRecordLength;

            Check.True(
                consumed <= buffer.Length,
                $"Consumed more data than available in buffer? {consumed} vs {buffer.Length}, {fieldsRead} f, {recordsRead} r, completed: {_isCompleted}, index: {_index}, line: {_lineNumber}, pos: {_position}, buffer: {buffer.Span.AsPrintableString()}"
            );

            chunk = new Chunk<T>(_index++, _lineNumber, _position, _options, buffer, _pool, owner, recordBuffer);
            _position += consumed;
            _lineNumber += recordsRead;
        }

        int leftover = buffer.Length - consumed;

        if (leftover > 0)
        {
            _pool.EnsureCapacity(ref _leftoverOwner, leftover);
            buffer.Slice(consumed).CopyTo(_leftoverOwner.Memory);
            _previousRead = _leftoverOwner.Memory.Slice(0, leftover);
        }
        else
        {
            _previousRead = ReadOnlyMemory<T>.Empty;
        }

        if (chunk is null)
        {
            // ownerships not passed to chunk
            recordBuffer.Dispose();
            owner.Dispose();
        }

        return chunk;
    }

    private IMemoryOwner<T> GetBufferForReading(out int startIndex)
    {
        int bufferSize = _bufferSize;

        // over half of the buffer is full but there is no fully formed record there?
        if (_previousRead.Length >= bufferSize / 2)
        {
            bufferSize = Math.Max(_previousRead.Length + bufferSize, bufferSize * 2);
        }

        Check.GreaterThan(bufferSize, _previousRead.Length);

        IMemoryOwner<T> memory = _pool.Rent<T>(bufferSize);
        _previousRead.CopyTo(memory.Memory);
        startIndex = _previousRead.Length;

        return memory;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _position, -1) == -1)
            return;

        _leftoverOwner?.Dispose();
        DisposeCore();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _position, -1) == -1)
            return;

        _leftoverOwner?.Dispose();
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SkipPreamble(ref Memory<T> buffer)
    {
        if (buffer.Span is [(byte)0xEF, (byte)0xBB, (byte)0xBF, ..])
        {
            buffer = buffer.Slice(3);
            _position += 3;
        }

        _needToSkipPreamble = false;
    }
}
