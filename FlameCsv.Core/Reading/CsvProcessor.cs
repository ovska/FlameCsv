using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Reading;

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        ReadNextRecord:
        if (!_dialect.TryGetLine(ref buffer, out ReadOnlySequence<T> lineSeq, out RecordMeta meta, isFinalBlock))
        {
            Unsafe.SkipInit(out value);
            return false;
        }

        // TODO: unify with other recordmeta throws
        if (meta.quoteCount % 2 != 0)
        {
            ThrowInvalidStringDelimiterException(in lineSeq);
        }

        ReadOnlyMemory<T> line = lineSeq.AsMemory(ref _multisegmentBuffer, _arrayPool);

        if (_skipPredicate?.Invoke(line, in _dialect) ?? false)
        {
            goto ReadNextRecord;
        }

        try
        {
            CsvEnumerationStateRef<T> state = new(
                dialect: in _dialect,
                record: line,
                remaining: line,
                isAtStart: true,
                meta: meta,
                array: ref _unescapeBuffer,
                arrayPool: _arrayPool,
                exposeContent: _exposeContent);

            value = _materializer.Parse(ref state);
            return true;
        }
        catch (Exception ex)
        {
            // this is treated as an unrecoverable exception
            if (ex is CsvFormatException)
            {
                ThrowInvalidFormatException(ex, line);
            }

            if ((_exceptionHandler?.Invoke(line, ex)) ?? false)
            {
                goto ReadNextRecord;
            }

            throw;
        }
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowInvalidFormatException(
        Exception innerException,
        ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format: {line.Span.AsPrintableString(_exposeContent, in _dialect)}",
            innerException);
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
