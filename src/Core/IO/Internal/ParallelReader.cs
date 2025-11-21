using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal sealed class Chunk<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    public long Position { get; }
    public ReadOnlyMemory<T> Data { get; }
    public RecordBuffer RecordBuffer { get; }

    private readonly ParallelReader<T> _reader;

    public Chunk(long position, ReadOnlyMemory<T> data, RecordBuffer recordBuffer, ParallelReader<T> reader)
    {
        Position = position;
        Data = data;
        RecordBuffer = recordBuffer;
        _reader = reader;
    }

    public bool TryPop(out CsvRecordRef<T> record)
    {
        if (RecordBuffer.TryPop(out RecordView view))
        {
            record = new(_reader, RecordBuffer, ref MemoryMarshal.GetReference(Data.Span), view);
            return true;
        }

        record = default;
        return false;
    }

    public void Dispose()
    {
        RecordBuffer.Dispose();
    }
}

internal sealed class ParallelTextReader : ParallelReader<char>
{
    private readonly TextReader _reader;

    public ParallelTextReader(TextReader reader, CsvOptions<char> options, CsvIOOptions ioOptions)
        : base(options, ioOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    protected override int ReadCore(Span<char> buffer)
    {
        return _reader.Read(buffer);
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        _reader.Dispose();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _reader.Dispose();
        return default;
    }
}

internal abstract class ParallelReader<T> : CsvReaderBase<T>, IEnumerable<Chunk<T>>, IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly int _bufferSize;

    private readonly CsvTokenizer<T>? _tokenizer;
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    private bool _isCompleted;

    private T[] _previousData;
    private int _previousRead;

    private int _recordBufferSize = 1024;

    private long _position;

    protected ParallelReader(CsvOptions<T> options, CsvIOOptions ioOptions)
        : base(options)
    {
        _options = options;
        _bufferSize = ioOptions.BufferSize;

        _previousData = [];
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
            Memory<T> memory = GetBufferForReading(out int startIndex);
            int read = ReadCore(memory.Slice(startIndex).Span);
            int totalRead = read + startIndex;

            if (read == 0)
            {
                _isCompleted = true;
            }

            if (TryAdvance(memory.Slice(0, totalRead), out Chunk<T>? chunk))
            {
                int leftover = totalRead - chunk.RecordBuffer.BufferedRecordLength;

                if (_previousData.Length < leftover)
                {
                    _previousData = new T[leftover];
                }

                memory.Slice(chunk.RecordBuffer.BufferedRecordLength, leftover).CopyTo(_previousData);
                _previousRead = leftover;

                return chunk;
            }
        }

        return null;
    }

    private Memory<T> GetBufferForReading(out int startIndex)
    {
        Memory<T> memory = new T[_bufferSize];
        startIndex = _previousRead;

        _previousData.AsMemory(0, _previousRead).CopyTo(memory);
        _previousRead = 0;

        return memory;
    }

    private bool TryAdvance(ReadOnlyMemory<T> memory, [NotNullWhen(true)] out Chunk<T>? chunk)
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

                chunk = new Chunk<T>(_position, memory, recordBuffer, this);
                _position += recordBuffer.BufferedRecordLength;
                return true;
            }
        }

        chunk = default;
        return false;
    }

    public void Dispose()
    {
        using (_unescapeAllocator)
        {
            DisposeCore();
        }
    }

    public async ValueTask DisposeAsync()
    {
        using (_unescapeAllocator)
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
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
