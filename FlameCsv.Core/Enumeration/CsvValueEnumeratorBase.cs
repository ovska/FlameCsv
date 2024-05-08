using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv.Enumeration;

public abstract class CsvValueEnumeratorBase<T, TValue> : IDisposable where T : unmanaged, IEquatable<T>
{
    public TValue Current => _current;

    public int Line => _line;
    public long Position => _position;

    protected readonly CsvDataReader<T> _data;

    private readonly CsvReadingContext<T> _context;
    private readonly CsvTypeMap<T, TValue>? _typeMap;
    private IMaterializer<T, TValue>? _materializer;

    private TValue _current;
    private long _position;
    private int _line;

    private T[]? _unescapeBuffer; // string unescaping

    internal CsvValueEnumeratorBase(in CsvReadingContext<T> context, CsvTypeMap<T, TValue> typeMap)
        : this(in context, null, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumeratorBase(in CsvReadingContext<T> context, IMaterializer<T, TValue>? materializer)
            : this(in context, materializer, null)
    {

    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumeratorBase(in CsvReadingContext<T> context) : this(in context, null, null)
    {
    }

    private CsvValueEnumeratorBase(
        in CsvReadingContext<T> context,
        IMaterializer<T, TValue>? materializer,
        CsvTypeMap<T, TValue>? typeMap)
    {
        _context = context;
        _materializer = materializer;
        _typeMap = typeMap;

        // Two buffers are needed, as the span being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _unescapeBuffer = null;

        _data = new();
        _current = default!;
    }

    protected bool TryRead(bool isFinalBlock)
    {
        ReadNextRecord:
        if (!_context.TryReadLine(_data, out ReadOnlyMemory<T> record, out RecordMeta meta, isFinalBlock))
        {
            return false;
        }

        if (meta.quoteCount % 2 != 0)
        {
            _context.ThrowForUnevenQuotes(record);
        }

        long position = _position;

        _line++;
        _position += record.Length + (!isFinalBlock).ToByte() * _context.Dialect.Newline.Length;

        if (_context.SkipRecord(record, _line, _context.HasHeader && _materializer is not null))
        {
            goto ReadNextRecord;
        }

        if (_materializer is null && TryReadHeader(record))
        {
            if (isFinalBlock)
            {
                return false;
            }

            goto ReadNextRecord;
        }

        try
        {
            CsvFieldReader<T> reader = new(
                record,
                in _context,
                stackalloc T[Token<T>.StackLength],
                ref _unescapeBuffer,
                meta.quoteCount,
                meta.escapeCount);

            _current = _materializer.Parse(ref reader);
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

            ThrowUnhandledException(ex, record, position);
            throw; // unreachable
        }
    }

    [MemberNotNull(nameof(_materializer))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Messages.HeaderProcessorSuppressionMessage)]
    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = Messages.HeaderProcessorSuppressionMessage)]
    private bool TryReadHeader(ReadOnlyMemory<T> record)
    {
        if (!_context.HasHeader)
        {
            _materializer = _typeMap is null
                ? _context.Options.GetMaterializer<T, TValue>()
                : _typeMap.GetMaterializer(in _context);
            return false;
        }

        var meta = _context.GetRecordMeta(record);
        var reader = new CsvFieldReader<T>(
            record,
            in _context,
            stackalloc T[Token<T>.StackLength],
            ref _unescapeBuffer,
            meta.quoteCount,
            meta.escapeCount);

        List<string> values = new(16);

        while (reader.TryReadNext(out ReadOnlySpan<T> field))
        {
            values.Add(_context.Options.GetAsString(field));
        }

        ReadOnlySpan<string> headers = values.AsSpan();

        _materializer = _typeMap is null
            ? _context.Options.CreateMaterializerFrom(_context.Options.GetHeaderBinder().Bind<TValue>(headers))
            : _typeMap.GetMaterializer(headers, in _context);
        return true;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidFormatException(Exception innerException, ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format. The record was on line {_line} at character " +
            $"position {_position} in the CSV. Record: {_context.AsPrintableString(line)}",
            innerException);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnhandledException(
        Exception innerException,
        ReadOnlyMemory<T> record,
        long position)
    {
        throw new CsvUnhandledException(
            $"Unhandled exception while reading records of type {typeof(TValue)} from the CSV. The record was on " +
            $"line {_line} at character position {position} in the CSV. Record: {_context.AsPrintableString(record)}",
            _line,
            position,
            innerException);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.ArrayPool.EnsureReturned(ref _unescapeBuffer);
            _context.ArrayPool.EnsureReturned(ref _data.MultisegmentBuffer);
            _data.Reader = default;
        }
    }
}
