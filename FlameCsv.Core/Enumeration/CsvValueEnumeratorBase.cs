using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Runtime;
using FlameCsv.Utilities;
using System.Diagnostics;
using System.Buffers;

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

    private IMemoryOwner<T>? _unescapeBuffer; // string unescaping
    protected readonly CsvParser<T> _parser;

    protected CsvValueEnumeratorBase(CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(options, null, typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
    }

    protected CsvValueEnumeratorBase(CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
            : this(options, materializer, null)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    [RUF(Messages.CompiledExpressions)]
    protected CsvValueEnumeratorBase(CsvOptions<T> options) : this(options, null, null)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    private CsvValueEnumeratorBase(
        CsvOptions<T> options,
        IMaterializer<T, TValue>? materializer,
        CsvTypeMap<T, TValue>? typeMap)
    {
        _parser = CsvParser<T>.Create(options);
        _materializer = materializer;
        _typeMap = typeMap;
        _current = default!;
    }

    protected bool TryRead(bool isFinalBlock)
    {
        ReadNextRecord:
        if (!_parser.TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta, isFinalBlock))
        {
            return false;
        }

        long position = _position;

        _line++;
        _position += line.Length + (isFinalBlock ? 0 : _parser._newlineLength);

        ReadOnlySpan<T> record = line.Span;

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
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Messages.HeaderProcessorSuppressionMessage)]
    private bool TryReadHeader(ReadOnlySpan<T> record)
    {
        Debug.Assert(_typeMap is not null || (RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled));

        if (!_parser.HasHeader)
        {
            _materializer = _typeMap is null
                ? _parser._options.GetMaterializer<T, TValue>()
                : _typeMap.BindMembers(_parser._options);
            return false;
        }

        StringScratch scratch = default;
        ValueListBuilder<string> list = new(scratch);

        var meta = _parser.GetRecordMeta(record);
        var reader = new CsvFieldReader<T>(
            _parser.Options,
            record,
            stackalloc T[Token<T>.StackLength],
            ref _unescapeBuffer,
            in meta);

        try
        {
            while (reader.MoveNext())
            {
                list.Append(_parser._options.GetAsString(reader.Current));
            }

            ReadOnlySpan<string> headers = list.AsSpan();

            _materializer = _typeMap is null
                ? _parser._options.CreateMaterializerFrom(_parser._options.GetHeaderBinder().Bind<TValue>(headers))
                : _typeMap.BindMembers(headers, _parser._options);
            return true;
        }
        finally
        {
            list.Dispose();
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidFormatException(Exception innerException, ReadOnlySpan<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format. The record was on line {_line} at character " +
            $"position {_position} in the CSV. Record: {_parser.AsPrintableString(line)}",
            innerException);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnhandledException(
        Exception innerException,
        ReadOnlySpan<T> record,
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
                _unescapeBuffer?.Dispose();
            }
        }
    }
}

[InlineArray(16)]
file struct StringScratch { public string? elem0; }
