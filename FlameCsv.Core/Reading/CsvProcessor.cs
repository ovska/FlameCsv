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
    public int Line => _line;
    public long Position => _position;

    private readonly CsvReadingContext<T> _context;
    private readonly IMaterializer<T, TValue> _materializer;

    private long _position;
    private int _line;

    private T[]? _unescapeBuffer; // string unescaping
    private T[]? _multisegmentBuffer; // long fragmented lines

    public CsvProcessor(in CsvReadingContext<T> context, IMaterializer<T, TValue>? materializer = null)
    {
        _context = context;
        _materializer = materializer ?? context.Options.GetMaterializer<T, TValue>();

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

        ReadOnlyMemory<T> record = lineSeq.AsMemory(ref _multisegmentBuffer, _context.ArrayPool);

        if (meta.quoteCount % 2 != 0)
        {
            _context.ThrowForUnevenQuotes(record);
        }

        _line++;
        _position += record.Length + (!isFinalBlock).ToByte() * _context.Dialect.Newline.Length;

        if (_context.SkipRecord(record, _line))
        {
            goto ReadNextRecord;
        }

        try
        {
            CsvEnumerationStateRef<T> state = new(in _context, record, ref _unescapeBuffer, meta);

            value = _materializer.Parse(ref state);
            return true;
        }
        catch (Exception ex)
        {
            // this is treated as an unrecoverable exception
            if (ex is CsvFormatException)
            {
                ThrowInvalidFormatException(ex, record);
            }

            if (_context.ExceptionIsHandled(record, _line, ex))
            {
                goto ReadNextRecord;
            }

            if (ex is not CsvParseException)
                ThrowUnhandledException(ex, record);

            throw;
        }
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowInvalidFormatException(Exception innerException, ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format, line {_line}, token position {_position}: " +
            _context.AsPrintableString(line),
            innerException);
    }

    [StackTraceHidden, DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowUnhandledException(Exception innerException, ReadOnlyMemory<T> line)
    {
        throw new CsvUnhandledException(
            $"Unhandled exception while parsing {typeof(TValue)} from the CSV. {_context.AsPrintableString(line)}",
            _line,
            _position,
            innerException);
    }

    public void Dispose()
    {
        _context.ArrayPool.EnsureReturned(ref _unescapeBuffer);
        _context.ArrayPool.EnsureReturned(ref _multisegmentBuffer);
    }
}
