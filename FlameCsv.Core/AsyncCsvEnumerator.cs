using System.Buffers;
using FlameCsv.Reading;

namespace FlameCsv;

public sealed class AsyncCsvEnumerator<T> : IAsyncEnumerable<CsvRecord<char>>, IAsyncEnumerator<CsvRecord<char>>
    where T : unmanaged, IEquatable<T>
{
    public CsvDialect<T> Dialect => _dialect;

    public CsvRecord<T> Current { get; private set; }

    CsvRecord<char> IAsyncEnumerator<CsvRecord<char>>.Current { get; }

    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvReaderOptions<T> _options;
    private readonly int? _columnCount;
    private readonly CsvDialect<T> _dialect;

    private readonly BufferOwner<T> _recordBuffer;
    private readonly BufferOwner<T> _multisegmentBuffer;

    private ReadOnlySequence<T> _data;
    private bool _readerCompleted;
    private long _position;
    private int _lineIndex;
    private CancellationToken _cancellationToken;

    internal AsyncCsvEnumerator(
        ICsvPipeReader<T> reader,
        CsvReaderOptions<T> options,
        int? columnCount)
    {
        _reader = reader;
        _options = options;
        _columnCount = columnCount;

        _dialect = new(options);
        _recordBuffer = new(options.ArrayPool);
        _multisegmentBuffer = new(options.ArrayPool);

        Current = default;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<bool>(_cancellationToken);

        if (TryMoveNextFromExistingBuffer())
            return new ValueTask<bool>(true);

        return MoveNextAsyncCore();
    }

    private async ValueTask<bool> MoveNextAsyncCore()
    {
        while (!_readerCompleted)
        {
            _reader.AdvanceTo(_data.Start, _data.End);

            var result = await _reader.ReadAsync(_cancellationToken);

            _data = result.Buffer;
            _readerCompleted = result.IsCompleted;

            if (TryMoveNextFromExistingBuffer())
                return true;
        }

        return false;
    }

    private bool TryMoveNextFromExistingBuffer()
    {
        if (LineReader.TryGetLine(in _dialect, ref _data, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock: false))
        {
            MoveNextImpl(in line, quoteCount);
            _position += _dialect.Newline.Length;
            return true;
        }

        if (_readerCompleted)
        {
            if (LineReader.TryGetLine(in _dialect, ref _data, out line, out quoteCount, isFinalBlock: true))
            {
                MoveNextImpl(in line, quoteCount);
                return true;
            }

            _position += Current.Data.Length;
            Current = default;
        }

        return false;
    }

    private void MoveNextImpl(in ReadOnlySequence<T> line, int quoteCount)
    {
        ReadOnlyMemory<T> memory;

        if (line.IsSingleSegment)
        {
            memory = line.First;
        }
        else
        {
            int length = (int)line.Length;
            Memory<T> buffer = _multisegmentBuffer.GetMemory(length);
            line.CopyTo(buffer.Span);
            memory = buffer;
        }

        Current = new CsvRecord<T>(
            memory,
            _options,
            _columnCount,
            quoteCount,
            _recordBuffer,
            _position,
            ++_lineIndex);

        _position += Current.Data.Length;
    }

    public async ValueTask DisposeAsync()
    {
        _multisegmentBuffer.Dispose();
        _recordBuffer.Dispose();
        await _reader.DisposeAsync();
    }

    public AsyncCsvEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    IAsyncEnumerator<CsvRecord<char>> IAsyncEnumerable<CsvRecord<char>>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
