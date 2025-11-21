using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal abstract class ParallelReader<T> : CsvReaderBase<T>, IParallelReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly MemoryPool<T> _memoryPool;
    private readonly int _bufferSize;

    private readonly CsvTokenizer<T>? _tokenizer;
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    private bool _isCompleted;

    private IMemoryOwner<T> _previousData;
    private int _previousRead;

    private int _recordBufferSize = 1024;

    private long _position;

    protected ParallelReader(CsvOptions<T> options, CsvIOOptions ioOptions)
        : base(options)
    {
        _memoryPool = options.Allocator;
        _bufferSize = ioOptions.BufferSize;

        _previousData = HeapMemoryOwner<T>.Empty;
        _previousRead = 0;
        _position = 0;

        _tokenizer = CsvTokenizer.Create(options);
        _scalarTokenizer = CsvTokenizer.CreateScalar(options);
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
                _memoryPool.EnsureCapacity(ref _previousData, leftover, copyOnResize: false);

                memory.Slice(chunk.RecordBuffer.BufferedRecordLength, leftover).CopyTo(_previousData.Memory);
                _previousRead = leftover;

                return chunk;
            }
        }

        return null;
    }

    private IMemoryOwner<T> GetBufferForReading(out int startIndex)
    {
        IMemoryOwner<T> memory = _memoryPool.Rent(_bufferSize);
        startIndex = _previousRead;

        _previousData.Memory.Slice(0, _previousRead).CopyTo(memory.Memory);
        _previousRead = 0;

        return memory;
    }

    private bool TryAdvance(IMemoryOwner<T> owner, ReadOnlyMemory<T> memory, [NotNullWhen(true)] out Chunk<T>? chunk)
    {
        RecordBuffer recordBuffer = new(_recordBufferSize);

        FieldBuffer destination = recordBuffer.GetUnreadBuffer(
            _tokenizer?.MinimumFieldBufferSize ?? 0,
            out int startIndex
        );

        ReadOnlySpan<T> data = memory.Span;

        int count = _tokenizer is null
            ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: _isCompleted)
            : _tokenizer.Tokenize(destination, startIndex, data);

        // read tail if there is no more data and the SIMD path can't read it
        if (count == 0 && _tokenizer is not null && _isCompleted)
        {
            count = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: true);
        }

        if (count > 0)
        {
            int recordsRead = recordBuffer.SetFieldsRead(count);

            if (recordsRead > 0)
            {
                // Adjust the next buffer's size based on utilization

                // If we're using more than 75% of the buffer capacity, grow it
                if (count > (_recordBufferSize * 3 / 4))
                {
                    _recordBufferSize = Math.Min(_recordBufferSize * 2, 4096 * 16);
                }
                // If we're using less than 25% of the buffer capacity, shrink it
                else if (count < (_recordBufferSize / 4))
                {
                    _recordBufferSize = Math.Max(_recordBufferSize / 2, 256);
                }

                chunk = new Chunk<T>(_position, memory, owner, recordBuffer, this);
                _position += recordBuffer.BufferedRecordLength;
                return true;
            }
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
        _unescapeAllocator.Dispose();
        DisposeCore();
    }

    public async ValueTask DisposeAsync()
    {
        if (_position == -1)
            return;

        _position = -1;

        _previousData.Dispose();
        _unescapeAllocator.Dispose();
        await DisposeAsyncCore().ConfigureAwait(false);
    }

    public IEnumerator<Chunk<T>> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Enumerator(ParallelReader<T> reader) : IEnumerator<Chunk<T>>
    {
        public Chunk<T> Current { get; private set; } = null!;

        public bool MoveNext() => (Current = reader.Read()!) is not null;

        object IEnumerator.Current => Current;

        void IEnumerator.Reset() => throw new NotSupportedException();

        void IDisposable.Dispose() { }
    }
}
