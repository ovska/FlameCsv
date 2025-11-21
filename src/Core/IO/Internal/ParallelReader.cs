using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal readonly struct SlimRecord<T>(ReadOnlyMemory<T> memory, RecordView view, RecordBuffer buffer) : ICsvRecord<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public ReadOnlySpan<T> this[int index]
    {
        get
        {
            ReadOnlySpan<T> data = memory.Span;
            ReadOnlySpan<uint> fields = view.GetFields(buffer);
            int start = Field.NextStart(fields[index]);
            uint field = fields[index + 1];

            if (view.IsFirst && index == 0)
            {
                start = 0;
            }

            return data[start..Field.End(field)];
        }
    }

    public int FieldCount => view.FieldCount;

    public ReadOnlySpan<T> Raw
    {
        get => throw new NotSupportedException();
    }
}

internal sealed class Chunk<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    public ReadOnlyMemory<T> Data { get; }
    public RecordBuffer RecordBuffer { get; }

    public Chunk(ReadOnlyMemory<T> data, RecordBuffer recordBuffer)
    {
        Data = data;
        RecordBuffer = recordBuffer;
    }

    public bool TryPop(out SlimRecord<T> record)
    {
        if (RecordBuffer.TryPop(out RecordView view))
        {
            record = new(Data, view, RecordBuffer);
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

internal abstract class ParallelReader<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly int _bufferSize;

    private readonly CsvTokenizer<T>? _tokenizer;
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    public bool IsCompleted { get; private set; }

    private T[] _previousData;
    private int _previousRead;

    private int _recordBufferSize = 368;

    protected ParallelReader(CsvOptions<T> options, CsvIOOptions ioOptions)
    {
        _options = options;
        _bufferSize = ioOptions.BufferSize;

        _previousData = [];
        _previousRead = 0;

        _tokenizer = CsvTokenizer.Create(options);
        _scalarTokenizer = CsvTokenizer.CreateScalar(options);
    }

    protected abstract int ReadCore(Span<T> buffer);
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);
    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeAsyncCore();

    public Chunk<T>? Read()
    {
        while (!IsCompleted || _previousRead > 0)
        {
            Memory<T> memory = new T[BitOperations.RoundUpToPowerOf2((uint)Math.Max(_bufferSize, _previousRead))];

            _previousData.AsMemory(0, _previousRead).CopyTo(memory);
            int read = ReadCore(memory.Slice(_previousRead).Span);
            int totalRead = read + _previousRead;
            _previousRead = 0;

            if (read == 0)
            {
                IsCompleted = true;
            }

            if (TryAdvance(memory, totalRead, out Chunk<T>? chunk))
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

    private bool TryAdvance(ReadOnlyMemory<T> memory, int read, [NotNullWhen(true)] out Chunk<T>? chunk)
    {
        Debug.Assert(read > 0);

        RecordBuffer recordBuffer = new(_recordBufferSize);
        ReadOnlySpan<T> data = memory.Span.Slice(0, read);

        FieldBuffer destination = recordBuffer.GetUnreadBuffer(_tokenizer!.MinimumFieldBufferSize, out int startIndex);

        int count = _tokenizer is null
            ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: false)
            : _tokenizer.Tokenize(destination, startIndex, data);

        if (count == 0 && IsCompleted)
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
                    _recordBufferSize = Math.Max(_recordBufferSize / 2, 64);
                }

                // copy tail to leftover buffer
                chunk = new Chunk<T>(memory, recordBuffer);
                return true;
            }
        }

        chunk = default;
        return false;
    }

    public void Dispose()
    {
        DisposeCore();
    }

    public ValueTask DisposeAsync()
    {
        return DisposeAsyncCore();
    }

    private sealed class Enumerator(ParallelReader<T> reader) : IEnumerator<Chunk<T>>
    {
        public Chunk<T> Current { get; private set; } = null!;

        public bool MoveNext() => (Current = reader.Read()!) is not null;

        object IEnumerator.Current => Current;

        void IEnumerator.Reset() => throw new NotSupportedException();

        void IDisposable.Dispose() { }
    }
}
