using System.Buffers;
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
    private readonly CsvDialect<T> _dialect;
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
        _dialect = new CsvDialect<T>(options);
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
        if (LineReader.TryGetLine(in _dialect, ref buffer, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock))
        {
            return TryReadOrSkipRecord(in line, quoteCount, out value);
        }

        Unsafe.SkipInit(out value);

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadOrSkipRecord(
        in ReadOnlySequence<T> lineSequence,
        int quoteCount,
        out TValue value)
    {
        ReadOnlyMemory<T> line;

        if (lineSequence.IsSingleSegment)
        {
            line = lineSequence.First;
        }
        else
        {

            Memory<T> buffer = new BufferOwner<T>(ref _multisegmentBuffer, _arrayPool).GetMemory((int)lineSequence.Length);
            lineSequence.CopyTo(buffer.Span);
            line = buffer;
        }

        ReadOnlySpan<T> lineSpan = line.Span;

        if (quoteCount % 2 != 0)
        {
            ThrowInvalidStringDelimiterException(lineSpan);
        }

        if (_skipPredicate is not null && _skipPredicate(lineSpan, in _dialect))
        {
            Unsafe.SkipInit(out value);
            return false;
        }

        try
        {
            CsvEnumerationStateRef<T> state = new(
                dialect: in _dialect,
                record: line,
                remaining: line,
                isAtStart: true,
                quoteCount: ref quoteCount,
                buffer: ref _unescapeBuffer,
                arrayPool: _arrayPool,
                exposeContent: _exposeContent);

            value = _materializer.Parse(ref state);
            return true;
        }
        catch (CsvFormatException ex)
        {
            CsvFormatException.Throw(ex, line.Span, _exposeContent, in _dialect);
        }
        catch (Exception ex)
        {
            if (_exceptionHandler?.Invoke(line.Span, ex) != true)
                throw;
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    private void ThrowInvalidStringDelimiterException(ReadOnlySpan<T> line)
    {
        CsvFormatException.Throw(
            "The data ended while there was a dangling string delimiter",
            line,
            _exposeContent,
            in _dialect);
    }

    public void Dispose()
    {
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
        _arrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
