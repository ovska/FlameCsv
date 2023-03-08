using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;
using FlameCsv.Runtime;

namespace FlameCsv.Readers;

internal struct CsvProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvTokens<T> _tokens;
    private readonly CsvCallback<T, bool>? _skipPredicate;
    private readonly CsvExceptionHandler<T>? _exceptionHandler;
    private readonly ICsvRowState<T, TValue> _state;
    private readonly int _columnCount;

    private readonly ArrayPool<T> _arrayPool;
    private T[]? _unescapeBuffer; // string unescaping
    private T[]? _multisegmentBuffer; // long fragmented lines, see TryReadColumns

    public CsvProcessor(
        CsvReaderOptions<T> options,
        ICsvRowState<T, TValue>? state = null)
    {
        _tokens = options.Tokens.ThrowIfInvalid();
        _skipPredicate = options.ShouldSkipRow;
        _exceptionHandler = options.ExceptionHandler;
        _arrayPool = options.ArrayPool ?? AllocatingArrayPool<T>.Instance;
        _state = state ?? options.BindToState<T, TValue>();
        _columnCount = _state.ColumnCount;

        // Two buffers are needed, as the ReadOnlySpan being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _unescapeBuffer = null;
        _multisegmentBuffer = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (LineReader.TryRead(in _tokens, ref buffer, out ReadOnlySequence<T> line, out int quoteCount))
        {
            if (line.IsSingleSegment)
            {
                return TryReadColumnSpan(line.FirstSpan, quoteCount, out value);
            }

            return TryReadColumns(in line, quoteCount, out value);
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
        int? quoteCount,
        out TValue value)
    {
        Debug.Assert(!line.IsSingleSegment);

        int length = (int)line.Length;

        if (Token<T>.CanStackalloc(length))
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, quoteCount, out value);
        }
        else
        {
            Span<T> buffer = new ValueBufferOwner<T>(ref _multisegmentBuffer, _arrayPool).GetSpan(length);
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, quoteCount, out value);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadColumnSpan(
        ReadOnlySpan<T> line,
        int? quoteCount,
        out TValue value)
    {
        try
        {
            if (_skipPredicate is null || !_skipPredicate(line, in _tokens))
            {
                var enumerator = new CsvColumnEnumerator<T>(
                    line,
                    in _tokens,
                    _columnCount,
                    quoteCount ?? line.Count(_tokens.StringDelimiter),
                    new ValueBufferOwner<T>(ref _unescapeBuffer, _arrayPool));

                value = _state.Parse(ref enumerator);
                return true;
            }
        }
        catch (Exception ex)
        {
            // Note: exception filter doesn't correctly propagate exceptions if handler rethrows
#pragma warning disable RCS1236 // Use exception filter.
            if (_exceptionHandler?.Invoke(line, ex) != true)
                throw;
#pragma warning restore RCS1236 // Use exception filter.
        }

        value = default!;
        return false;
    }

    public void Dispose()
    {
        _state.Dispose();
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
        _arrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
