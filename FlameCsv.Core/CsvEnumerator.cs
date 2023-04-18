using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public sealed class CsvEnumerator<T> :
    IEnumerable<CsvRecord<T>>, IEnumerator<CsvRecord<T>>,
    IAsyncEnumerable<CsvRecord<T>>, IAsyncEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    public CsvRecord<T> Current { get; private set; }

    private ReadOnlySequence<T> _data;
    private readonly CsvReaderOptions<T> _options;
    private readonly int? _columnCount;
    private readonly CsvDialect<T> _dialect;

    private readonly BufferOwner<T> _recordBuffer;
    private readonly BufferOwner<T> _multisegmentBuffer;

    private long _position;
    private int _lineIndex;
    private CancellationToken _cancellationToken;

    public CsvEnumerator(
        ReadOnlySequence<T> data,
        CsvReaderOptions<T> options,
        int? columnCount)
    {
        _data = data;
        _options = options;
        _columnCount = columnCount;
        _position = default;
        _lineIndex = default;

        _dialect = new(options);
        _recordBuffer = new(options.ArrayPool);
        _multisegmentBuffer = new(options.ArrayPool);

        Current = default;
    }

    public CsvEnumerator<T> GetEnumerator() => this;
    
    public CsvEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    public bool MoveNext()
    {
        if (LineReader.TryGetLine(in _dialect, ref _data, out var line, out int quoteCount, isFinalBlock: false))
        {
            MoveNextImpl(in line, quoteCount, hasNewline: true);
            _position += _dialect.Newline.Length;
            return true;
        }

        // If data didn't have trailing newline
        if (!_data.IsEmpty)
        {
            MoveNextImpl(in _data, quoteCount: null, hasNewline: false);
            _data = default; // consume all data
            return true;
        }

        _position += Current.Data.Length;
        Current = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveNextImpl(in ReadOnlySequence<T> line, int? quoteCount, bool hasNewline)
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

    public void Dispose()
    {
        _multisegmentBuffer.Dispose();
        _recordBuffer.Dispose();
    }

    IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

    IAsyncEnumerator<CsvRecord<T>> IAsyncEnumerable<CsvRecord<T>>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }

    object IEnumerator.Current => Current;
}
