using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides convenience methods for writing CSV records.
/// </summary>
[PublicAPI]
public sealed class CsvWriter<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Options instance of this writer.
    /// </summary>
    public CsvOptions<T> Options => _inner.Options;

    /// <summary>
    /// Whether to automatically check if the writer needs to be flushed after each record.<br/>
    /// The default value is <c>true</c>.
    /// </summary>
    /// <seealso cref="ICsvBufferWriter{T}.NeedsFlush"/>
    public bool AutoFlush { get; set; }

    /// <summary>
    /// Whether to automatically ensure a trailing newline is written if not already present
    /// when the writer completes without an error.<br/>
    /// The default value is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// The trailing newline check does not validate the field count.
    /// </remarks>
    public bool EnsureTrailingNewline { get; set; }

    /// <summary>
    /// Whether a header record has been written.
    /// </summary>
    /// <remarks>
    /// This property is set to <c>true</c> after the first call to <c>WriteHeader</c>.
    /// It has no effect on the usage of the writer, but can be helpful when writing values in a streaming manner.
    /// </remarks>
    public bool HeaderWritten { get; private set; }

    /// <summary>
    /// Field count required for each record, if set. Terminating a record (with <see cref="NextRecord"/> or completion)
    /// will throw a <see cref="CsvWriteException"/> if the record is not empty, and the field count does not match.
    /// </summary>
    /// <remarks>
    /// If not <c>null</c>, set to the field count of the first non-empty record written if <see cref="CsvOptions{T}.ValidateFieldCount"/>
    /// is <c>true</c>. You can set this property to <c>null</c> to reset the field count validation in this case.
    /// </remarks>
    public int? ExpectedFieldCount { get; set; }

    /// <summary>
    /// Dematerializers indexed either by the type (reflection), or the typemap instance (sourcegen).
    /// </summary>
    private readonly IDictionary<object, object> _dematerializerCache;

    /// <summary>
    /// Previous cache key and value used to avoid re-reading the cache.
    /// </summary>
    private object? _previousKey;

    /// <summary>
    /// Previous cache value used to avoid re-reading the cache.
    /// </summary>
    private object? _previousValue;

    /// <summary>
    /// 0-based index of the current field. Reset to 0 at the start of each record.
    /// </summary>
    public int FieldIndex { get; private set; }

    /// <summary>
    /// 1-based index of the current line/record. Incremented after each newline.
    /// </summary>
    /// <remarks>
    /// Newlines in quoted fields/strings are not counted, this property represents the logical CSV record index.
    /// </remarks>
    public int LineIndex { get; private set; }

    /// <summary>
    /// Whether the writer has completed (disposed).
    /// </summary>
    public bool IsCompleted { get; private set; }

    private readonly CsvFieldWriter<T> _inner;
    private readonly bool _validateFieldCount;

    /// <summary>
    /// Initializes a new writer instance.
    /// </summary>
    /// <param name="inner">Field writer instance to write to</param>
    public CsvWriter([HandlesResourceDisposal] CsvFieldWriter<T> inner)
    {
        Throw.IfDefaultStruct(inner.Writer is null, typeof(CsvFieldWriter<T>));

        _inner = inner;
        _validateFieldCount = inner.Options.ValidateFieldCount;
        LineIndex = 1;

        AutoFlush = true;
        EnsureTrailingNewline = true;

        // omit the overhead of the concurrent dict if hot reload is not active
        if (!HotReloadService.IsActive)
        {
            _dematerializerCache = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        }
        else
        {
            _dematerializerCache = new ConcurrentDictionary<object, object>(ReferenceEqualityComparer.Instance);

            HotReloadService.RegisterForHotReload(
                this,
                static state =>
                {
                    if (state is CsvWriter<T> { IsCompleted: false } @this)
                    {
                        @this._previousKey = null;
                        @this._previousValue = null;
                        @this._dematerializerCache.Clear();
                    }
                }
            );
        }
    }

    /// <summary>
    /// Writes a field with the preceding delimiter if needed.
    /// </summary>
    /// <remarks>Converter will be retrieved from options.</remarks>
    /// <param name="value">Value to write</param>
    /// <typeparam name="TField">Field type that will be converted</typeparam>
    [RDC(Messages.ConverterOverload), RUF(Messages.ConverterOverload)]
    public void WriteField<TField>(TField? value)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteField(Options.GetConverter<TField?>(), value);
        FieldIndex++;
    }

    /// <summary>
    /// Writes a field with the preceding delimiter if needed.
    /// </summary>
    /// <param name="converter">Converter instance to write the value with</param>
    /// <param name="value">Value to write</param>
    /// <typeparam name="TField">Field type that will be converted</typeparam>
    public void WriteField<TField>(CsvConverter<T, TField> converter, TField? value)
    {
        ArgumentNullException.ThrowIfNull(converter);
        WriteDelimiterIfNeeded();
        _inner.WriteField(converter, value);
        FieldIndex++;
    }

    /// <summary>
    /// Writes a field with the preceding delimiter if needed.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="skipEscaping">Whether no escaping should be performed, use with care</param>
    [OverloadResolutionPriority(1000)] // prefer writing span instead of generic TField
    public void WriteField(ReadOnlySpan<T> value, bool skipEscaping = false)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteRaw(value, skipEscaping);
        FieldIndex++;
    }

    /// <inheritdoc cref="WriteField(ReadOnlySpan{T},bool)"/>
    [OverloadResolutionPriority(500)] // prefer writing this instead of string TField
    public void WriteField(ReadOnlySpan<char> chars, bool skipEscaping = false)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteText(chars, skipEscaping);
        FieldIndex++;
    }

    /// <summary>
    /// Writes the fields with the preceding delimiter if needed.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.<br/>
    /// <c>null</c> values are written as empty fields.
    /// </remarks>
    /// <param name="values">Values to write</param>
    /// <param name="skipEscaping">Whether no escaping should be performed, use with care</param>
    public void WriteFields(ReadOnlySpan<string> values, bool skipEscaping = false)
    {
        foreach (var value in values)
        {
            WriteDelimiterIfNeeded();
            _inner.WriteText(value, skipEscaping);
            FieldIndex++;
        }
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="AutoFlush"/> is true.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has completed</exception>
    public void NextRecord()
    {
        ObjectDisposedException.ThrowIf(IsCompleted, this);

        ValidateFieldCount();

        _inner.WriteNewline();
        FieldIndex = 0;
        LineIndex++;

        if (AutoFlush && _inner.Writer.NeedsFlush)
            _inner.Writer.Flush();
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="AutoFlush"/> is true.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the flush</param>
    /// <exception cref="ObjectDisposedException">The writer has completed</exception>
    public ValueTask NextRecordAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return ObjectDisposedValueTask();

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        ValidateFieldCount();

        _inner.WriteNewline();
        FieldIndex = 0;
        LineIndex++;

        if (AutoFlush && _inner.Writer.NeedsFlush)
            return _inner.Writer.FlushAsync(cancellationToken);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes the value to the current line using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.<br/>
    /// Throws on <c>null</c> values.
    /// </remarks>
    /// <param name="value">Value to write</param>
    /// <exception cref="ArgumentNullException"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public int WriteRecord<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteDelimiterIfNeeded();

        var dematerializer = GetDematerializerAndIncrementFieldCount<TRecord>();
        dematerializer.Write(in _inner, value);

        return dematerializer.FieldCount;
    }

    /// <summary>
    /// Writes the value to the current line using the type map.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.<br/>
    /// Throws on <c>null</c> values.
    /// </remarks>
    /// <param name="typeMap">Type map to use for writing</param>
    /// <param name="value">Value to write</param>
    /// <returns>Number of fields written</returns>
    /// <exception cref="ArgumentNullException"/>
    public int WriteRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(value);
        WriteDelimiterIfNeeded();

        var dematerializer = GetDematerializerAndIncrementFieldCount(typeMap);
        dematerializer.Write(in _inner, value);

        return dematerializer.FieldCount;
    }

    /// <summary>
    /// Writes the provided record to the writer.
    /// </summary>
    /// <remarks>
    /// If the record uses the same options-instance as the writer, the raw record is written directly.
    /// </remarks>
    /// <param name="record">Record to write</param>
    /// <returns>Number of fields written</returns>
    public int WriteRecord(in CsvRecord<T> record)
    {
        record.EnsureValid();

        // check if the records have the exact same dialect
        if (Options.DialectEquals(record.Options))
        {
            WriteDelimiterIfNeeded();
            _inner.WriteRaw(record.RawRecord, skipEscaping: true);
        }
        else
        {
            foreach (var field in record)
            {
                WriteDelimiterIfNeeded();
                _inner.WriteRaw(field);
            }
        }

        return record.FieldCount;
    }

    /// <summary>
    /// Writes the specified fields from the provided record to the writer.
    /// </summary>
    /// <remarks>
    /// If the record uses the same options-instance as the writer, the raw fields are written directly.<br/>
    /// Fields are written in the order they are specified in <paramref name="fieldIds"/>.
    /// </remarks>
    /// <param name="record">Record to write</param>
    /// <param name="fieldIds">Identifiers of fields to write</param>
    /// <returns>Number of fields written</returns>
    public int WriteRecord(in CsvRecord<T> record, scoped ReadOnlySpan<CsvFieldIdentifier> fieldIds)
    {
        record.EnsureValid();

        if (fieldIds.IsEmpty)
        {
            return 0;
        }

        CsvRecordRef<T> recordRef = new(in record._slice);

        bool writeRaw = Options.DialectEquals(record.Options);

        foreach (var id in fieldIds)
        {
            int index = record.GetFieldIndex(id);
            WriteDelimiterIfNeeded();

            if (writeRaw)
            {
                _inner.WriteRaw(recordRef.GetRawSpan(index), skipEscaping: true);
            }
            else
            {
                _inner.WriteRaw(recordRef[index], skipEscaping: false);
            }

            FieldIndex++;
        }

        return fieldIds.Length;
    }

    /// <summary>
    /// Writes the provided record to the writer.
    /// </summary>
    /// <remarks>
    /// If the record uses the same options-instance as the writer, the raw record is written directly.
    /// </remarks>
    /// <param name="record">Record to write</param>
    /// <returns>Number of fields written</returns>
    public int WriteRecord(CsvPreservedRecord<T> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // check if the records have the exact same dialect
        if (Options.DialectEquals(record.Options))
        {
            WriteDelimiterIfNeeded();
            _inner.WriteRaw(record.RawRecord.Span, skipEscaping: true);
        }
        else
        {
            foreach (var segment in record._fields)
            {
                WriteDelimiterIfNeeded();
                _inner.WriteRaw(segment.AsSpanUnsafe(), skipEscaping: false);
            }
        }

        return record.FieldCount;
    }

    /// <summary>
    /// Writes the header from the provided record.
    /// </summary>
    /// <param name="record">Record to write the headers from</param>
    /// <returns>Number of fields written</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the writer was created with <see cref="CsvOptions{T}.HasHeader"/> set to <c>false</c>
    /// or if the record does not have a header.
    /// </exception>
    public int WriteHeader(in CsvRecord<T> record)
    {
        record.EnsureValid();

        CsvHeader? header = record.Header;

        if (!Options.HasHeader || header is null)
        {
            Throw.NotSupported_CsvHasNoHeader();
        }

        return WriteHeader(header.Values.AsSpan());
    }

    /// <summary>
    /// Writes the provided header values.
    /// </summary>
    /// <param name="values">Header values</param>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.<br/>
    /// <c>null</c> values are written as empty fields.
    /// </remarks>
    /// <returns>Number of fields written</returns>
    public int WriteHeader(params ReadOnlySpan<string> values)
    {
        foreach (var value in values)
        {
            WriteDelimiterIfNeeded();
            _inner.WriteText(value);
            FieldIndex++;
        }

        HeaderWritten = true;
        return values.Length;
    }

    /// <summary>
    /// Writes the header for <typeparamref name="TRecord"/>
    /// to the current line using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <returns>Number of fields written</returns>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public int WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        WriteDelimiterIfNeeded();

        var dematerializer = GetDematerializerAndIncrementFieldCount<TRecord>();
        dematerializer.WriteHeader(in _inner);

        HeaderWritten = true;
        return dematerializer.FieldCount;
    }

    /// <summary>
    /// Writes the header for <typeparamref name="TRecord"/> to the current line using the type map.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <param name="typeMap">Type map to use for writing</param>
    /// <returns>Number of fields written</returns>
    public int WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        WriteDelimiterIfNeeded();

        var dematerializer = GetDematerializerAndIncrementFieldCount(typeMap);
        dematerializer.WriteHeader(in _inner);

        HeaderWritten = true;
        return dematerializer.FieldCount;
    }

    /// <summary>
    /// Completes the writer, flushing any remaining data if <paramref name="exception"/> is null.<br/>
    /// Multiple completions are no-ops.
    /// </summary>
    /// <param name="exception">
    /// Observed exception when writing the data.
    /// If not null, the final buffer is not flushed and the exception is rethrown.
    /// </param>
    public void Complete(Exception? exception = null)
    {
        if (!IsCompleted)
        {
            IsCompleted = true;
            HotReloadService.UnregisterForHotReload(this);
            WriteTrailingNewlineIfNeeded(exception, CancellationToken.None);
            _inner.Writer.Complete(exception);
        }
    }

    /// <summary>
    /// Completes the writer, flushing any remaining data if <paramref name="exception"/> is null.<br/>
    /// Multiple completions are no-ops.
    /// </summary>
    /// <param name="exception">
    /// Observed exception when writing the data, passed to the inner <see cref="ICsvBufferWriter{T}"/>.
    /// If not null, the final buffer is not flushed and the exception is rethrown.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    public async ValueTask CompleteAsync(Exception? exception = null, CancellationToken cancellationToken = default)
    {
        if (!IsCompleted)
        {
            IsCompleted = true;
            HotReloadService.UnregisterForHotReload(this);
            WriteTrailingNewlineIfNeeded(exception, cancellationToken);
            await _inner.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }

    private void WriteTrailingNewlineIfNeeded(Exception? exception, CancellationToken cancellationToken)
    {
        if (
            exception is null
            && EnsureTrailingNewline
            && (FieldIndex != 0 || LineIndex == 1) // write newline if current line was empty, or nothing was written yet
            && !cancellationToken.IsCancellationRequested
        )
        {
            // don't call NextRecord as it can trigger an unnecessary auto-flush
            _inner.WriteNewline();
            FieldIndex = 0;
            LineIndex++;
        }
    }

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the writer has completed (see <see cref="Complete"/>).
    /// </exception>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(IsCompleted, this);
        _inner.Writer.Flush();
    }

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel flushing</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the writer has completed (see <see cref="CompleteAsync"/>).
    /// </exception>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return IsCompleted ? ObjectDisposedValueTask() : _inner.Writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using the type map,
    /// and pre-increments <see cref="FieldIndex"/> by <see cref="IDematerializer{T,TValue}.FieldCount"/>.
    /// </summary>
    /// <param name="typeMap">Type map instance</param>
    /// <typeparam name="TRecord">Type to write</typeparam>
    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeMap,
            state: typeMap,
            factory: static (options, state) => ((CsvTypeMap<T, TRecord>)state!).GetDematerializer(options)
        );
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using <see cref="CsvOptions{T}.TypeBinder"/>,
    /// and pre-increments <see cref="FieldIndex"/> by <see cref="IDematerializer{T,TValue}.FieldCount"/>.
    /// </summary>
    /// <typeparam name="TRecord">Type to write</typeparam>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<
        [DAM(Messages.ReflectionBound)] TRecord
    >()
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeof(TRecord),
            state: null,
            factory: static (options, _) => options.TypeBinder.GetDematerializer<TRecord>()
        );
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using the specified cache key, and increments
    /// the field count so the calling method can write the record directly.
    /// </summary>
    /// <param name="cacheKey">Type or TypeMap to cache the dematerializer by</param>
    /// <param name="state">State for the dematerializer factory</param>
    /// <param name="factory">Factory to create a dematerializer if none exists yet</param>
    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCountCore<TRecord>(
        object cacheKey,
        object? state,
        [RequireStaticDelegate] Func<CsvOptions<T>, object?, IDematerializer<T, TRecord>> factory
    )
    {
        Debug.Assert(cacheKey is Type or CsvTypeMap<T, TRecord>);

        IDematerializer<T, TRecord> dematerializer;

        // optimize for consecutive calls with the same type
        if (ReferenceEquals(_previousKey, cacheKey))
        {
            Debug.Assert(_previousValue is IDematerializer<T, TRecord>);
            dematerializer = Unsafe.As<IDematerializer<T, TRecord>>(_previousValue!);
        }
        else
        {
            if (_dematerializerCache.TryGetValue(cacheKey, out object? cached))
            {
                Debug.Assert(cached is IDematerializer<T, TRecord>);
                dematerializer = Unsafe.As<IDematerializer<T, TRecord>>(cached);
            }
            else
            {
                _dematerializerCache[cacheKey] = dematerializer = factory(Options, state);
            }

            _previousKey = cacheKey;
            _previousValue = dematerializer;
        }

        FieldIndex += dematerializer.FieldCount;
        return dematerializer;
    }

    /// <summary>
    /// Writes a delimiter if the current field index is not 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteDelimiterIfNeeded()
    {
        if (FieldIndex > 0)
            _inner.WriteDelimiter();
    }

    /// <summary>
    /// Validates the current record's field count if <see cref="CsvOptions{T}.ValidateFieldCount"/>
    /// is <c>true</c>. Called when moving to the next record.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateFieldCount()
    {
        if (FieldIndex > 0 && (_validateFieldCount || ExpectedFieldCount.HasValue))
        {
            if (ExpectedFieldCount is null)
            {
                ExpectedFieldCount = FieldIndex;
            }
            else if (ExpectedFieldCount.GetValueOrDefault() != FieldIndex)
            {
                ThrowHelper.InvalidFieldCount(LineIndex, ExpectedFieldCount.GetValueOrDefault(), FieldIndex);
            }
        }
    }

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        Complete();
    }

    /// <inheritdoc/>
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return CompleteAsync();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValueTask ObjectDisposedValueTask()
    {
        return ValueTask.FromException(new ObjectDisposedException(typeof(CsvWriter<T>).Name));
    }
}

file static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    public static void InvalidFieldCount(int lineIndex, int expected, int actual)
    {
        throw new CsvWriteException($"Invalid field count at line {lineIndex}. Expected {expected}, got {actual}.")
        {
            LineNumber = lineIndex,
        };
    }
}
