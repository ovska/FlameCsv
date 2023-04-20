using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _exposeContent = options.AllowContentInExceptions;

        _materializer = materializer ?? options.GetMaterializer<T, TValue>();

        // Two buffers are needed, as the span being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _unescapeBuffer = null;
        _multisegmentBuffer = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (RFC4180Mode<T>.TryGetLine(in _dialect, ref buffer, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock))
        {
            return TryReadOrSkipRecord(in line, quoteCount, out value);
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadOrSkipRecord(in ReadOnlySequence<T> lineSequence, int quoteCount, out TValue value)
    {
        if (quoteCount % 2 != 0)
        {
            ThrowInvalidStringDelimiterException(lineSequence);
        }

        ReadOnlyMemory<T> line;
        ReadOnlySpan<T> lineSpan;

        if (lineSequence.IsSingleSegment)
        {
            line = lineSequence.First;
            lineSpan = line.Span;
        }
        else
        {
            int length = (int)lineSequence.Length;
            _arrayPool.EnsureCapacity(ref _multisegmentBuffer, length);
            lineSequence.CopyTo(_multisegmentBuffer);
            line = _multisegmentBuffer.AsMemory(0, length);
            lineSpan = _multisegmentBuffer.AsSpan(0, length);
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
                quoteCount: quoteCount,
                buffer: ref _unescapeBuffer,
                arrayPool: _arrayPool,
                exposeContent: _exposeContent);

            value = _materializer.Parse(ref state);
            return true;
        }
        catch (CsvFormatException ex)
        {
            ThrowInvalidFormatException(ex, lineSpan);
        }
        catch (Exception ex)
        {
            if (_exceptionHandler?.Invoke(lineSpan, ex) != true)
                throw;
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowInvalidFormatException(
        Exception exception,
        ReadOnlySpan<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format: {line.AsPrintableString(_exposeContent, in _dialect)}",
            exception);
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowInvalidStringDelimiterException(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _arrayPool);

        throw new CsvFormatException(
            "The data ended while there was a dangling string delimiter: " +
            view.Span.AsPrintableString(_exposeContent, in _dialect));
    }

    public void Dispose()
    {
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
        _arrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
