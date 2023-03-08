using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public struct CsvEnumerator<T> : IEnumerable<CsvRecord<T>>, IEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    public CsvRecord<T> Current { get; private set; }

    private ReadOnlySequence<T> _data;
    private readonly CsvReaderOptions<T> _options;
    private readonly int? _columnCount;

    private readonly BufferOwner<T> _recordBuffer;
    private readonly BufferOwner<T> _multisegmentBuffer;

    private long _position;
    private int _lineIndex;

    internal CsvEnumerator(
        ReadOnlySequence<T> data,
        CsvReaderOptions<T> options,
        int? columnCount)
    {
        options.tokens.ThrowIfInvalid();

        _data = data;
        _options = options;
        _columnCount = columnCount;
        _position = default;
        _lineIndex = default;

        _recordBuffer = new(options.ArrayPool);
        _multisegmentBuffer = new(options.ArrayPool);

        Current = default;
    }

    public readonly CsvEnumerator<T> GetEnumerator() => this;

    public bool MoveNext()
    {
        if (LineReader.TryRead(in _options.tokens, ref _data, out var line, out int quoteCount))
        {
            MoveNextImpl(in line, quoteCount, hasNewline: true);
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
        if (hasNewline) _position += _options.tokens.NewLine.Length;
    }

    public readonly void Dispose()
    {
        _multisegmentBuffer.Dispose();
        _recordBuffer.Dispose();
    }

    readonly IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    readonly void IEnumerator.Reset() => throw new NotSupportedException();
    readonly object IEnumerator.Current => Current;
}
