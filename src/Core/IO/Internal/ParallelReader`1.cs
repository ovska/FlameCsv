using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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

    private ReadOnlyMemory<T> _previousRead;
    private IMemoryOwner<T> _leftoverOwner;

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
        _leftoverOwner = HeapMemoryOwner<T>.Empty;
        _position = 0;
        _lineNumber = 0;
        _index = 0;

        (_scalarTokenizer, _tokenizer) = options.GetTokenizers();
    }

    protected abstract int ReadCore(Span<T> buffer);
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);
    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeAsyncCore();

    public Chunk<T>? Read()
    {
        while (!_isCompleted || !_previousRead.IsEmpty)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);

            try
            {
                Memory<T> memory = owner.Memory;
                int read = ReadCore(memory.Slice(startIndex).Span);

                if (read == 0)
                {
                    _isCompleted = true;
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

        return null;
    }

    public async ValueTask<Chunk<T>?> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (!_isCompleted || !_previousRead.IsEmpty)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);

            try
            {
                Memory<T> memory = owner.Memory;
                int read = await ReadAsyncCore(memory.Slice(startIndex), cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    _isCompleted = true;
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

        return null;
    }

    private Chunk<T>? TryReadCore(IMemoryOwner<T> owner, Memory<T> buffer)
    {
        RecordBuffer recordBuffer = new(_recordBufferSize);

        Span<uint> destination = recordBuffer.GetUnreadBuffer(
            _tokenizer?.MaxFieldsPerIteration ?? 0,
            out int startIndex
        );

        ReadOnlySpan<T> data = buffer.Span;

        Read:
        int fieldsRead = _tokenizer is null
            ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: _isCompleted)
            : _tokenizer.Tokenize(destination, startIndex, data);

        if (fieldsRead < 0)
        {
            // fall back to scalar parser for pathological data
            Check.NotNull(_tokenizer);
            _tokenizer = null;
            goto Read;
        }

        int recordsRead = recordBuffer.SetFieldsRead(fieldsRead);

        // read tail if there is no more data and the SIMD path can't read it
        if (recordsRead == 0 && _tokenizer is not null && _isCompleted)
        {
            fieldsRead = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: true);
            recordsRead = recordBuffer.SetFieldsRead(fieldsRead);
        }

        Chunk<T>? chunk = null;
        int consumed;

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

            chunk = new Chunk<T>(_index++, _lineNumber, _position, _options, buffer, _pool, owner, recordBuffer);
            consumed = recordBuffer.BufferedRecordLength;

            Check.True(
                consumed <= buffer.Length,
                $"Consumed more data than available in buffer? {consumed} vs {buffer.Length} (read {fieldsRead} fields and {recordsRead} records)"
            );

            _position += consumed;
            _lineNumber += recordsRead;
        }
        else
        {
            // no records here, store all data for next read
            consumed = 0;
            owner.Dispose(); // ownership not passed to caller
        }

        _pool.EnsureCapacity(ref _leftoverOwner, buffer.Length - consumed);
        buffer.Slice(consumed).CopyTo(_leftoverOwner.Memory);
        _previousRead = _leftoverOwner.Memory.Slice(0, buffer.Length - consumed);

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

        _leftoverOwner.Dispose();
        DisposeCore();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _position, -1) == -1)
            return;

        _leftoverOwner.Dispose();
        await DisposeAsyncCore().ConfigureAwait(false);
    }
}
