using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Reading;

// exception filter doesn't correctly propagate exceptions if handler rethrows
#pragma warning disable RCS1236 // Use exception filter.

internal struct CsvProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvTokens<T> _tokens;
    private readonly CsvCallback<T, bool>? _skipPredicate;
    private readonly CsvExceptionHandler<T>? _exceptionHandler;
    private readonly IMaterializer<T, TValue> _materializer;
    private readonly bool _exposeContent;

    private readonly ArrayPool<T> _arrayPool;
    private T[]? _unescapeBuffer; // string unescaping
    private T[]? _multisegmentBuffer; // long fragmented lines, see TryReadColumns

    public CsvProcessor(
        CsvReaderOptions<T> options,
        IMaterializer<T, TValue>? materializer = null)
    {
        _tokens = options.Tokens.ThrowIfInvalid();
        _skipPredicate = options.ShouldSkipRow;
        _exceptionHandler = options.ExceptionHandler;
        _arrayPool = options.ArrayPool ?? AllocatingArrayPool<T>.Instance;
        _exposeContent = options.AllowContentInExceptions;

        _materializer = materializer ?? options.GetMaterializer<T, TValue>();

        // Two buffers are needed, as the ReadOnlySpan being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _unescapeBuffer = null;
        _multisegmentBuffer = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        Unsafe.SkipInit(out value);

        if (LineReader.TryGetLine(in _tokens, ref buffer, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock))
        {
            if (line.IsSingleSegment)
            {
                return TrySkipOrReadColumnSpan(line.FirstSpan, quoteCount, out value);
            }

            return TryReadColumns(in line, quoteCount, out value);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining of common path
    private bool TryReadColumns(
        in ReadOnlySequence<T> line,
        int quoteCount,
        out TValue value)
    {
        Debug.Assert(!line.IsSingleSegment);

        int length = (int)line.Length;

        if (Token<T>.CanStackalloc(length))
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            return TrySkipOrReadColumnSpan(buffer, quoteCount, out value);
        }
        else
        {
            Span<T> buffer = new ValueBufferOwner<T>(ref _multisegmentBuffer, _arrayPool).GetSpan(length);
            line.CopyTo(buffer);
            return TrySkipOrReadColumnSpan(buffer, quoteCount, out value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TrySkipOrReadColumnSpan(
        ReadOnlySpan<T> line,
        int quoteCount,
        out TValue value)
    {
        Unsafe.SkipInit(out value);

        if (quoteCount % 2 != 0)
        {
            ThrowInvalidStringDelimiterException(line);
        }

        if (_skipPredicate is null || !_skipPredicate(line, in _tokens))
        {
            return TryReadColumnSpan(line, quoteCount, out value);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadColumnSpan(
        ReadOnlySpan<T> line,
        int quoteCount,
        out TValue value)
    {
        Unsafe.SkipInit(out value);

        try
        {
            var enumerator = new CsvColumnEnumerator<T>(
                line,
                in _tokens,
                _materializer.ColumnCount,
                quoteCount,
                new ValueBufferOwner<T>(ref _unescapeBuffer, _arrayPool));

            value = _materializer.Parse(ref enumerator);
            return true;
        }
        catch (CsvFormatException ex)
        {
            CsvFormatException.Throw(ex, line, _exposeContent, in _tokens);
        }
        catch (Exception ex)
        {
            if (_exceptionHandler?.Invoke(line, ex) != true)
                throw;
        }

        return false;
    }

    private void ThrowInvalidStringDelimiterException(ReadOnlySpan<T> line)
    {
        CsvFormatException.Throw(
            "The data ended while there was a dangling string delimiter",
            line,
            _exposeContent,
            in _tokens);
    }

    public void Dispose()
    {
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
        _arrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
