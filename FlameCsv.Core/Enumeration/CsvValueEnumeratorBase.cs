using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Runtime;
using FlameCsv.Utilities;
using System.Diagnostics;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// An enumerator that parses instances of <typeparamref name="TValue"/> from CSV records.
/// </summary>
/// <remarks>
/// If the options are configured to read a header record, it will be processed first before any records are yielded.<br/>
/// This class is not thread-safe, and should not be used concurrently.<br/>
/// The enumerator should always be disposed after use, either explicitly or using <c>foreach</c>.
/// </remarks>
[MustDisposeResource]
public abstract class CsvValueEnumeratorBase<T, TValue> : IDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Value parsed from the current CSV record.
    /// </summary>
    public TValue Current { get; private set; }

    /// <inheritdoc cref="CsvRecordEnumeratorBase{T}.Line"/>
    public int Line { get; private set; }

    /// <inheritdoc cref="CsvRecordEnumeratorBase{T}.Position"/>
    public long Position { get; private set; }

    /// <summary>
    /// Delegate that is called when an exception is thrown while parsing class records.
    /// If the delegate returns <see langword="true"/>, the faulty record is skipped.
    /// </summary>
    /// <remarks>
    /// <see cref="CsvFormatException"/> is not handled as it represents structurally invalid CSV.
    /// </remarks>
    public CsvExceptionHandler<T>? ExceptionHandler { get; init; }

    private readonly CsvTypeMap<T, TValue>? _typeMap;
    private IMaterializer<T, TValue>? _materializer;
    private string[]? _headersArray;

    [HandlesResourceDisposal] private protected readonly CsvParser<T> _parser;

    private protected CsvValueEnumeratorBase(CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(options, null, typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
    }

    private protected CsvValueEnumeratorBase(CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
        : this(options, materializer, null)
    {
    }

    [RUF(Messages.CompiledExpressions)]
    private protected CsvValueEnumeratorBase(CsvOptions<T> options) : this(options, null, null)
    {
    }

    private CsvValueEnumeratorBase(
        CsvOptions<T> options,
        IMaterializer<T, TValue>? materializer,
        CsvTypeMap<T, TValue>? typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);

        _parser = CsvParser.Create(options);
        _materializer = materializer;
        _typeMap = typeMap;
        Current = default!;
    }

    private protected bool TryRead(bool isFinalBlock)
    {
    ReadNextRecord:
        if (!_parser.TryReadLine(out CsvLine<T> line, isFinalBlock))
        {
            return false;
        }

        long position = Position;

        Line++;
        Position += line.RecordLength + (isFinalBlock ? 0 : _parser._newline.Length);

        if (_parser.Options._recordCallback is { } callback)
        {
            bool skip = false;
            bool headerRead = _parser.Options._hasHeader && _materializer is not null;

            CsvRecordCallbackArgs<T> args = new(
                line,
                _headersArray,
                Line,
                position,
                ref skip,
                ref headerRead);
            callback(in args);

            if (!headerRead && _parser.Options._hasHeader) _materializer = null; // null to re-read headers
            if (skip) goto ReadNextRecord;
        }

        if (_materializer is null && TryReadHeader(in line))
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
            MetaFieldReader<T> reader = new(in line, stackalloc T[Token<T>.StackLength]);
            Current = _materializer.Parse(ref reader);
            return true;
        }
        catch (Exception ex)
        {
            // this is treated as an unrecoverable exception
            if (ex is CsvFormatException)
            {
                ThrowInvalidFormatException(ex, in line);
            }

            var handler = ExceptionHandler;

            if (handler is not null)
            {
                CsvExceptionHandlerArgs<T> args = new(line, _headersArray, ex, Line, position);

                if (handler(in args))
                {
                    goto ReadNextRecord;
                }
            }

            ThrowUnhandledException(ex, in line, position);
            throw; // unreachable
        }
    }

    [MemberNotNull(nameof(_materializer))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Messages.HeaderProcessorSuppressionMessage)]
    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = Messages.HeaderProcessorSuppressionMessage)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = Messages.HeaderProcessorSuppressionMessage)]
    private bool TryReadHeader(ref readonly CsvLine<T> record)
    {
        Debug.Assert(
            _typeMap is not null || (RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled));

        if (!_parser.Options.HasHeader)
        {
            _materializer = _typeMap is null
                ? _parser.Options.GetMaterializer<T, TValue>()
                : _typeMap.GetMaterializer(_parser.Options);
            return false;
        }

        StringScratch scratch = default;
        using ValueListBuilder<string> list = new(scratch);

        MetaFieldReader<T> reader = new(in record, stackalloc T[Token<T>.StackLength]);
        Span<char> charBuffer = stackalloc char[128];

        for (int field = 0; field < reader.FieldCount; field++)
        {
            list.Append(CsvHeader.Get(_parser.Options, reader[field], charBuffer));
        }

        ReadOnlySpan<string> headers = list.AsSpan();

        _materializer = _typeMap is null
            ? _parser.Options.TypeBinder.GetMaterializer<TValue>(headers)
            : _typeMap.GetMaterializer(headers, _parser.Options);

        // we need a copy of the headers for the callbacks
        if (ExceptionHandler is not null || _parser.Options._recordCallback is not null)
        {
            _headersArray = headers.ToArray();
        }

        return true;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidFormatException(Exception innerException, in CsvLine<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format. The record was on line {Line} at character " +
            $"position {Position} in the CSV. Record: {line.Data.Span.AsPrintableString()}",
            innerException);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnhandledException(
        Exception innerException,
        in CsvLine<T> line,
        long position)
    {
        throw new CsvUnhandledException(
            $"Unhandled exception while reading records of type {typeof(TValue).FullName} from the CSV. The record was on " +
            $"line {Line} at character position {position} in the CSV. Record: " +
            line.Data.Span.AsPrintableString(),
            Line,
            position,
            innerException);
    }

    /// <summary>
    /// Disposes the underlying data source and internal states, and returns pooled memory.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parser.Dispose();
        }
    }
}
