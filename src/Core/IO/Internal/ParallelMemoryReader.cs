using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

internal sealed class ParallelMemoryReader<T> : IParallelReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvTokenizer<T>? _tokenizer;
    private readonly CsvScalarTokenizer<T> _scalarTokenizer;

    private readonly CsvOptions<T> _options;
    private readonly ReadOnlyMemory<T> _data;
    private int _position;
    private int _index;

    private readonly IBufferPool _bufferPool;

    public ParallelMemoryReader(ReadOnlyMemory<T> data, CsvOptions<T> options, IBufferPool? bufferPool)
    {
        _data = data;
        _position = 0;
        _index = 0;
        _bufferPool = bufferPool ?? DefaultBufferPool.Instance;

        _options = options;
        (_scalarTokenizer, _tokenizer) = options.GetTokenizers();
    }

    public Chunk<T>? Read()
    {
        if (_position < _data.Length)
        {
            RecordBuffer recordBuffer = new();

            Span<uint> destination = recordBuffer.GetUnreadBuffer(
                _tokenizer?.MaxFieldsPerIteration ?? 0,
                out int startIndex
            );

            ReadOnlyMemory<T> remaining = _data.Slice(_position);
            ReadOnlySpan<T> data = remaining.Span;

            int fieldsRead = _tokenizer is null
                ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: false)
                : _tokenizer.Tokenize(destination, startIndex, data);

            int recordsRead = recordBuffer.SetFieldsRead(fieldsRead);

            // read tail if there is no more data and/or the SIMD path can't read it
            if (recordsRead == 0)
            {
                fieldsRead = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: true);
                recordsRead = recordBuffer.SetFieldsRead(fieldsRead);
                _position = _data.Length;
            }

            if (recordsRead > 0)
            {
                Chunk<T> chunk = new(_index++, _options, remaining, _bufferPool, owner: null, recordBuffer);
                _position += recordBuffer.BufferedRecordLength;
                return chunk;
            }
        }

        return null;
    }

    public ValueTask<Chunk<T>?> ReadAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<Chunk<T>?>(cancellationToken)
            : new ValueTask<Chunk<T>?>(Read());
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => default;
}
