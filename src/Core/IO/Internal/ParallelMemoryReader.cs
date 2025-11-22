using System.Collections;
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

    public ParallelMemoryReader(ReadOnlyMemory<T> data, CsvOptions<T> options)
    {
        _data = data;
        _position = 0;
        _index = 0;

        _options = options;
        _tokenizer = CsvTokenizer.Create(options);
        _scalarTokenizer = CsvTokenizer.CreateScalar(options);
    }

    public Chunk<T>? Read()
    {
        if (_position < _data.Length)
        {
            RecordBuffer recordBuffer = new();

            FieldBuffer destination = recordBuffer.GetUnreadBuffer(
                _tokenizer?.MinimumFieldBufferSize ?? 0,
                out int startIndex
            );

            ReadOnlyMemory<T> remaining = _data.Slice(_position);
            ReadOnlySpan<T> data = remaining.Span;

            int count = _tokenizer is null
                ? _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: false)
                : _tokenizer.Tokenize(destination, startIndex, data);

            // read tail if there is no more data and the SIMD path can't read it
            if (count == 0)
            {
                count = _scalarTokenizer.Tokenize(destination, startIndex, data, readToEnd: true);
                _position = _data.Length;
            }

            if (count > 0)
            {
                int recordsRead = recordBuffer.SetFieldsRead(count);

                if (recordsRead > 0)
                {
                    Chunk<T> chunk = new(_index++, _options, remaining, owner: null, recordBuffer);
                    _position += recordBuffer.BufferedRecordLength;
                    return chunk;
                }
            }
        }

        return null;
    }

    public IEnumerator<Chunk<T>> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() { }

    public ValueTask DisposeAsync() => default;

    private sealed class Enumerator(ParallelMemoryReader<T> reader) : IEnumerator<Chunk<T>>
    {
        public Chunk<T> Current { get; private set; } = null!;

        public bool MoveNext() => (Current = reader.Read()!) is not null;

        object IEnumerator.Current => Current;

        void IEnumerator.Reset() => throw new NotSupportedException();

        void IDisposable.Dispose() { }
    }
}
