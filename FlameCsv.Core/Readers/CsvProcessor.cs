using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Readers.Internal;
using FlameCsv.Runtime;

namespace FlameCsv.Readers;

internal readonly struct CsvProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private static readonly int StackallocThreshold = Unsafe.SizeOf<byte>() * 256 / Unsafe.SizeOf<T>();

    /// <summary>Shared buffer for string unescaping in the data.</summary>
    private readonly BufferOwner<T> _enumeratorBuffer;

    /// <summary>Shared buffer for long segment-fragmented lines.</summary>
    private readonly BufferOwner<T> _multisegmentBuffer;

    private readonly CsvTokens<T> _tokens;
    private readonly CsvCallback<T, bool>? _skipPredicate;
    private readonly ICsvRowState<T, TValue> _state;
    private readonly int _columnCount;

    public CsvProcessor(CsvReaderOptions<T> readerOptions, ICsvRowState<T, TValue>? state = null)
    {
        _tokens = readerOptions.Tokens;
        _skipPredicate = readerOptions.ShouldSkipRow;
        _state = state ?? readerOptions.BindToState<TValue>();
        _columnCount = _state.ColumnCount;

        // Two buffers are needed, as the ReadOnlySpan being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _enumeratorBuffer = new(readerOptions.Security);
        _multisegmentBuffer = new(readerOptions.Security);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (LineReader.TryRead(
                in _tokens,
                ref buffer,
                out ReadOnlySequence<T> line,
                out int stringDelimiterCount))
        {
            if (line.IsSingleSegment)
            {
                return TryReadColumnSpan(line.FirstSpan, stringDelimiterCount, out value);
            }

            return TryReadColumns(in line, stringDelimiterCount, out value);
        }

        value = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadRemaining(in ReadOnlySequence<T> remaining, out TValue value)
    {
        Debug.Assert(!remaining.IsEmpty);

        if (remaining.IsSingleSegment)
        {
            return TryReadColumnSpan(remaining.FirstSpan, null, out value);
        }

        return TryReadColumns(in remaining, null, out value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadColumns(
        in ReadOnlySequence<T> line,
        int? stringDelimiterCount,
        out TValue value)
    {
        Debug.Assert(!line.IsSingleSegment);

        int length = (int)line.Length;

        if (length <= StackallocThreshold)
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, stringDelimiterCount, out value);
        }
        else
        {
            Span<T> buffer = _multisegmentBuffer.GetSpan(length);
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, stringDelimiterCount, out value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadColumnSpan(
        ReadOnlySpan<T> line,
        int? stringDelimiterCount,
        out TValue value)
    {
        if (_skipPredicate is null || !_skipPredicate(line, in _tokens))
        {
            var enumerator = new CsvColumnEnumerator<T>(
                line,
                in _tokens,
                _columnCount,
                stringDelimiterCount ?? line.Count(_tokens.StringDelimiter),
                _enumeratorBuffer);

            value = _state.Parse(ref enumerator);
            return true;
        }

        value = default!;
        return false;
    }

    public void Dispose()
    {
        _state.Dispose();
        _enumeratorBuffer.Dispose();
        _multisegmentBuffer.Dispose();
    }
}
