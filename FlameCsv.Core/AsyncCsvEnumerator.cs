using System.Buffers;
using FlameCsv.Reading;

namespace FlameCsv;

public sealed class AsyncCsvEnumerator<T> : IAsyncEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    public CsvDialect<T> Dialect => _dialect;
    public CsvRecord<T> Current { get; private set; }
    public int Line { get; private set; }
    public long Position { get; private set; }

    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvReaderOptions<T> _options;
    private readonly CsvDialect<T> _dialect;
    private readonly CsvEnumerationState<T> _state;
    private readonly BufferOwner<T> _multisegmentBuffer;
    private readonly CancellationToken _cancellationToken;

    private ReadOnlySequence<T> _data; // current buffer
    private bool _readerCompleted; // last call to ReadAsync returned IsCompleted=true

    internal AsyncCsvEnumerator(ICsvPipeReader<T> reader, CsvReaderOptions<T> options, CancellationToken cancellationToken)
    {
        _reader = reader;
        _options = options;
        _dialect = new(options);
        _state = new(options);
        _multisegmentBuffer = new(options.ArrayPool);
        _cancellationToken = cancellationToken;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<bool>(_cancellationToken);

        if (TryMoveNextSync())
            return new ValueTask<bool>(true);

        return MoveNextAsyncCore();
    }

    private async ValueTask<bool> MoveNextAsyncCore()
    {
        while (!_readerCompleted)
        {
            _reader.AdvanceTo(_data.Start, _data.End);

            (_data, _readerCompleted) = await _reader.ReadAsync(_cancellationToken);

            if (TryMoveNextSync())
                return true;
        }

        return false;
    }

    private bool TryMoveNextSync()
    {
        if (LineReader.TryGetLine(in _dialect, ref _data, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock: false))
        {
            MoveNextImpl(in line, quoteCount);
            Position += _dialect.Newline.Length;
            return true;
        }

        if (_readerCompleted)
        {
            if (LineReader.TryGetLine(in _dialect, ref _data, out line, out quoteCount, isFinalBlock: true))
            {
                MoveNextImpl(in line, quoteCount);
                return true;
            }

            Position += Current.Data.Length;
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

        Current = new CsvRecord<T>(Position, ++Line, memory, _options, quoteCount, _state);
        Position += memory.Length;
    }

    public async ValueTask DisposeAsync()
    {
        _multisegmentBuffer.Dispose();
        _state.Dispose();
        await _reader.DisposeAsync();
    }
}
