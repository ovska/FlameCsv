using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

public struct CsvEnumerator<T> : IEnumerable<CsvRecord<T>>, IEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    public CsvRecord<T> Current { get; private set; }

    private ReadOnlySequence<T> _data;
    private readonly CsvReaderOptions<T> _options;
    private readonly int? _columnCount;

    private readonly BufferOwner<T> _recordBuffer;
    private readonly BufferOwner<T> _multisegmentBuffer;

    /// <summary>
    /// Start index of the current line in the data.
    /// First line starts at 0.
    /// Equals length of the data after enumeration.
    /// </summary>
    public long Position { get; private set; }

    /// <summary>
    /// 1-based index of the line.
    /// </summary>
    public int Line { get; private set; }

    internal CsvEnumerator(
        ReadOnlySequence<T> data,
        CsvReaderOptions<T> options,
        int? columnCount)
    {
        _data = data;
        _options = options;
        _columnCount = columnCount;
        Position = default;
        Line = default;

        _recordBuffer = new();
        _multisegmentBuffer = new();

        Current = default;
    }

    public CsvEnumerator<T> GetEnumerator() => this;

    public bool MoveNext()
    {
        if (LineReader.TryRead(in _options.tokens, ref _data, out var line, out int quoteCount))
        {
            Position += Current.Line.Length;
            Line++;

            Current = GetRecord(in line, quoteCount);
            return true;
        }

        // If data didn't have trailing newline
        if (!_data.IsEmpty)
        {
            Position += Current.Line.Length;
            Line++;

            Current = GetRecord(in _data, quoteCount: null);
            _data = default; // consume all data
            return true;
        }

        Position += Current.Line.Length; // 0 if uninitialized
        Current = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CsvRecord<T> GetRecord(in ReadOnlySequence<T> line, int? quoteCount)
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

        return new CsvRecord<T>(memory, _options, _columnCount, quoteCount, _recordBuffer);
    }

    public void Dispose()
    {
        _multisegmentBuffer.Dispose();
        _recordBuffer.Dispose();
    }

    IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    void IEnumerator.Reset() => throw new NotSupportedException();
    object IEnumerator.Current => Current;
}
