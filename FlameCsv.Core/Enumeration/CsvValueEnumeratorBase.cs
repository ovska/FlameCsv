﻿using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Runtime;
using FlameCsv.Utilities;
using System.Diagnostics;
using System.Buffers;
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

    private readonly CsvTypeMap<T, TValue>? _typeMap;
    private IMaterializer<T, TValue>? _materializer;

    [HandlesResourceDisposal]
    private protected readonly CsvParser<T> _parser;
    private IMemoryOwner<T>? _unescapeBuffer; // string unescaping

    private protected CsvValueEnumeratorBase(CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
        : this(options, null, typeMap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
    }

    private protected CsvValueEnumeratorBase(CsvOptions<T> options, IMaterializer<T, TValue>? materializer)
        : this(options, materializer, null)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    [RUF(Messages.CompiledExpressions)]
    private protected CsvValueEnumeratorBase(CsvOptions<T> options) : this(options, null, null)
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
        Position += line.Value.Length + (isFinalBlock ? 0 : _parser._newline.Length);

        if (_parser.SkipRecord(in line, Line, _parser.Options._hasHeader && _materializer is null))
        {
            goto ReadNextRecord;
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
            CsvFieldReader<T> reader = new(
                _parser.Options,
                in line,
                stackalloc T[Token<T>.StackLength],
                ref _unescapeBuffer);

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

            if (_parser.ExceptionIsHandled(in line, Line, ex))
            {
                goto ReadNextRecord;
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
                : _typeMap.BindMembers(_parser.Options);
            return false;
        }

        StringScratch scratch = default;
        ValueListBuilder<string> list = new(scratch);

        var reader = new CsvFieldReader<T>(
            _parser.Options,
            in record,
            stackalloc T[Token<T>.StackLength],
            ref _unescapeBuffer);

        try
        {
            while (reader.MoveNext())
            {
                list.Append(_parser.Options.GetAsString(reader.Current));
            }

            ReadOnlySpan<string> headers = list.AsSpan();

            _materializer = _typeMap is null
                ? _parser.Options.CreateMaterializerFrom(_parser.Options.GetHeaderBinder().Bind<TValue>(headers))
                : _typeMap.BindMembers(headers, _parser.Options);
            return true;
        }
        finally
        {
            list.Dispose();
            reader.Dispose();
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidFormatException(Exception innerException, in CsvLine<T> line)
    {
        throw new CsvFormatException(
            $"The CSV was in an invalid format. The record was on line {Line} at character "
            + $"position {Position} in the CSV. Record: {_parser.Options.AsPrintableString(line.Value.Span)}",
            innerException);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnhandledException(
        Exception innerException,
        in CsvLine<T> line,
        long position)
    {
        throw new CsvUnhandledException(
            $"Unhandled exception while reading records of type {typeof(TValue)} from the CSV. The record was on "
            + $"line {Line} at character position {position} in the CSV. Record: "
            + _parser.Options.AsPrintableString(line.Value.Span),
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
            using (_parser)
            {
                _unescapeBuffer?.Dispose();
            }
        }
    }
}
