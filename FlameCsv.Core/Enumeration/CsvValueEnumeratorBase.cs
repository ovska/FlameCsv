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

    private readonly CsvTypeMap<T, TValue>? _typeMap;
    private IMaterializer<T, TValue>? _materializer;

    private TValue _current;
    private long _position;
    private int _line;

    private T[]? _unescapeBuffer; // string unescaping
    protected readonly CsvParser<T> _parser;

    internal CsvValueEnumeratorBase(CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(options, null, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumeratorBase(CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
            : this(options, materializer, null)
    {

    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueEnumeratorBase(CsvOptions<T> options) : this(options, null, null)
    {
    }

    private CsvValueEnumeratorBase(
        CsvOptions<T> options,
        IMaterializer<T, TValue>? materializer,
        CsvTypeMap<T, TValue>? typeMap)
    {
        _parser = CsvParser<T>.Create(options);
        _materializer = materializer;
        _typeMap = typeMap;

        _unescapeBuffer = null;

        _current = default!;
    }

    protected bool TryRead(bool isFinalBlock)
    {
        ReadNextRecord:
        if (!_parser.TryReadLine(out ReadOnlyMemory<T> record, out CsvRecordMeta meta, isFinalBlock))
        {
            return false;
        }

        long position = _position;

        _line++;
        _position += record.Length + (!isFinalBlock).ToByte() * _parser._newlineLength;

        if (_parser.SkipRecord(record, _line, _parser.HasHeader ? _materializer is not null : null))
        {
            goto ReadNextRecord;
        }

        if (_materializer is null && TryReadHeader(record))
        {
            // csv only had the header
            if (isFinalBlock)
            {
                return false;
            }

            goto ReadNextRecord;
        }

        try
        {
            CsvFieldReader<T> reader = new(
                _parser.Options,
                record,
                stackalloc T[Token<T>.StackLength],
                ref _unescapeBuffer,
                in meta);

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

            if (_parser.ExceptionIsHandled(record, _line, ex))
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
        if (!_parser.HasHeader)
        {
            _materializer = _typeMap is null
                ? _parser._options.GetMaterializer<T, TValue>()
                : _typeMap.BindMembers(_parser._options);
            return false;
        }

        var meta = _parser.GetRecordMeta(record);
        var reader = new CsvFieldReader<T>(
            _parser.Options,
            record,
            stackalloc T[Token<T>.StackLength],
            ref _unescapeBuffer,
            in meta);

        List<string> values = new(16);

        while (reader.TryReadNext(out ReadOnlySpan<T> field))
        {
            values.Add(_parser._options.GetAsString(field));
        }

        ReadOnlySpan<string> headers = values.AsSpan();

        _materializer = _typeMap is null
            ? _parser._options.CreateMaterializerFrom(_parser._options.GetHeaderBinder().Bind<TValue>(headers))
            : _typeMap.BindMembers(headers, _parser._options);
        return true;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidFormatException(Exception innerException, ReadOnlyMemory<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format. The record was on line {_line} at character " +
            $"position {_position} in the CSV. Record: {_parser.AsPrintableString(line)}",
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
            $"line {_line} at character position {position} in the CSV. Record: {_parser.AsPrintableString(record)}",
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
            using (_parser)
            {
                _parser._arrayPool.EnsureReturned(ref _unescapeBuffer);
            }
        }
    }
}
