using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv;

public sealed class CsvEnumerator<T> : IEnumerator<CsvRecord<T>>, IAsyncEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    public CsvRecord<T> Current { get; private set; }
    public int Line { get; private set; }
    public long Position { get; private set; }

    private ReadOnlySequence<T> _data;
    private readonly CsvReaderOptions<T> _options;
    private readonly CsvDialect<T> _dialect;

    private readonly CsvEnumerationState<T> _state;
    private readonly BufferOwner<T> _multisegmentBuffer;
    private readonly CancellationToken _cancellationToken;

    public CsvEnumerator(
        ReadOnlySequence<T> data,
        CsvReaderOptions<T> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        _data = data;
        _options = options;
        _dialect = new(options);
        _state = new(options);
        _multisegmentBuffer = new(options.ArrayPool);
        _cancellationToken = cancellationToken;
    }

    public bool MoveNext()
    {
        if (LineReader.TryGetLine(in _dialect, ref _data, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock: false))
        {
            MoveNextImpl(in line, quoteCount);
            Position += _dialect.Newline.Length; // increment position after record has been initialized
            return true;
        }

        if (LineReader.TryGetLine(in _dialect, ref _data, out line, out quoteCount, isFinalBlock: true))
        {
            MoveNextImpl(in line, quoteCount);
            return true;
        }

        Position += Current.Data.Length;
        Current = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var buffer = _multisegmentBuffer.GetMemory(length);
            line.CopyTo(buffer.Span);
            memory = buffer;
        }

        Current = new CsvRecord<T>(Position, ++Line, memory, _options, quoteCount, _state);

        // increment position _after_ the record has been initialized
        Position += memory.Length;
    }

    public void Dispose()
    {
        _multisegmentBuffer.Dispose();
        _state.Dispose();
    }

    void IEnumerator.Reset() => throw new NotSupportedException();

    ValueTask<bool> IAsyncEnumerator<CsvRecord<T>>.MoveNextAsync()
    {
        return !_cancellationToken.IsCancellationRequested
            ? new(MoveNext())
            : ValueTask.FromCanceled<bool>(_cancellationToken);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Dispose();
        return default;
    }

    object IEnumerator.Current => Current;
}
