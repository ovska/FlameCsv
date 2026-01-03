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

    private IMemoryOwner<T> _previousData;
    private int _previousRead;

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

        _previousData = HeapMemoryOwner<T>.Empty;
        _previousRead = 0;
        _position = 0;
        _lineNumber = 1;
        _index = 0;

        (_scalarTokenizer, _tokenizer) = options.GetTokenizers();
    }

    protected abstract int ReadCore(Span<T> buffer);
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);
    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeAsyncCore();

    public Chunk<T>? Read()
    {
        while (!_isCompleted || _previousRead > 0)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);
            Memory<T> memory = owner.Memory;
            int read = ReadCore(memory.Slice(startIndex).Span);
            int totalRead = read + startIndex;

            if (read == 0)
            {
                _isCompleted = true;
            }

            if (TryAdvance(owner, memory.Slice(0, totalRead), out Chunk<T>? chunk))
            {
                int leftover = totalRead - chunk.RecordBuffer.BufferedRecordLength;
                _pool.EnsureCapacity(ref _previousData, leftover, copyOnResize: false);

                memory.Slice(chunk.RecordBuffer.BufferedRecordLength, leftover).CopyTo(_previousData.Memory);
                _previousRead = leftover;

                return chunk;
            }

            // keep leftover for next read
            if (totalRead > 0)
            {
                _pool.EnsureCapacity(ref _previousData, totalRead, copyOnResize: false);
                memory.Slice(0, totalRead).CopyTo(_previousData.Memory);
            }

            // TODO: throw here and test that the exception bubbles up correctly

            _previousRead = totalRead;

            // ownership not passed to Chunk, dispose
            owner.Dispose();
        }

        return null;
    }

    public async ValueTask<Chunk<T>?> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (!_isCompleted || _previousRead > 0)
        {
            IMemoryOwner<T> owner = GetBufferForReading(out int startIndex);
            Memory<T> memory = owner.Memory;
            int read = await ReadAsyncCore(memory.Slice(startIndex), cancellationToken).ConfigureAwait(false);
            int totalRead = read + startIndex;

            if (read == 0)
            {
                _isCompleted = true;
            }

            if (TryAdvance(owner, memory.Slice(0, totalRead), out Chunk<T>? chunk))
            {
                int leftover = totalRead - chunk.RecordBuffer.BufferedRecordLength;
                _pool.EnsureCapacity(ref _previousData, leftover, copyOnResize: false);

                memory.Slice(chunk.RecordBuffer.BufferedRecordLength, leftover).CopyTo(_previousData.Memory);
                _previousRead = leftover;

                return chunk;
            }

            // keep leftover for next read
            if (totalRead > 0)
            {
                _pool.EnsureCapacity(ref _previousData, totalRead, copyOnResize: false);
                memory.Slice(0, totalRead).CopyTo(_previousData.Memory);
            }

            _previousRead = totalRead;

            // TODO: throw here and test that the exception bubbles up correctly

            // ownership not passed to Chunk, dispose
            owner.Dispose();
        }

        return null;
    }

    private IMemoryOwner<T> GetBufferForReading(out int startIndex)
    {
        int bufferSize = _bufferSize;

        // over half of the buffer is full but there is no fully formed record there?
        if (_previousRead >= bufferSize / 2)
        {
            bufferSize *= 2;
        }

        IMemoryOwner<T> memory = _pool.Rent<T>(bufferSize);
        startIndex = _previousRead;

        _previousData.Memory.Slice(0, _previousRead).CopyTo(memory.Memory);
        _previousRead = 0;

        return memory;
    }

    private bool TryAdvance(IMemoryOwner<T> owner, ReadOnlyMemory<T> memory, [NotNullWhen(true)] out Chunk<T>? chunk)
    {
        RecordBuffer recordBuffer = new(_recordBufferSize);

        Span<uint> destination = recordBuffer.GetUnreadBuffer(
            _tokenizer?.MaxFieldsPerIteration ?? 0,
            out int startIndex
        );

        ReadOnlySpan<T> data = memory.Span;

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

            chunk = new Chunk<T>(_index++, _lineNumber, _position, _options, memory, _pool, owner, recordBuffer);
            _position += recordBuffer.BufferedRecordLength;
            _lineNumber += recordsRead;
            return true;
        }

        chunk = default;
        return false;
    }

    public void Dispose()
    {
        if (_position == -1)
            return;

        _position = -1;

        _previousData.Dispose();
        DisposeCore();
    }

    public async ValueTask DisposeAsync()
    {
        if (_position == -1)
            return;

        _position = -1;

        _previousData.Dispose();
        await DisposeAsyncCore().ConfigureAwait(false);
    }
}
