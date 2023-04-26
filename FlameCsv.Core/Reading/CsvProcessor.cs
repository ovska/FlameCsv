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
    private readonly CsvReadingContext<T> _context;
    private readonly IMaterializer<T, TValue> _materializer;

    private T[]? _unescapeBuffer; // string unescaping
    private T[]? _multisegmentBuffer; // long fragmented lines

    public CsvProcessor(in CsvReadingContext<T> context, IMaterializer<T, TValue> materializer)
    {
        _context = context;
        _materializer = materializer;

        // Two buffers are needed, as the span being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _unescapeBuffer = null;
        _multisegmentBuffer = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        ReadNextRecord:
        if (!_context.TryGetLine(ref buffer, out ReadOnlySequence<T> lineSeq, out RecordMeta meta, isFinalBlock))
        {
            Unsafe.SkipInit(out value);
            return false;
        }

        ReadOnlyMemory<T> line = lineSeq.AsMemory(ref _multisegmentBuffer, _context.arrayPool);

        if (meta.quoteCount % 2 != 0)
        {
            _context.ThrowForUnevenQuotes(line);
        }

        if (_context.SkipRecord(line))
        {
            goto ReadNextRecord;
        }

        try
        {
            CsvEnumerationStateRef<T> state = new(in _context, line, ref _unescapeBuffer, meta);

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

            if (_context.ExceptionIsHandled(line, ex))
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
            $"The CSV was in an invalid format: {_context.AsPrintableString(line)}",
            innerException);
    }

    public void Dispose()
    {
        _context.arrayPool.EnsureReturned(ref _unescapeBuffer);
        _context.arrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
