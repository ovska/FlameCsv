using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

/// <inheritdoc cref="CsvWriter{T}"/>
[PublicAPI]
public class CsvAsyncWriter<T> : IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Options instance of this writer.
    /// </summary>
    public CsvOptions<T> Options => _inner.Options;

    /// <summary>
    /// Whether to automatically check if the writer needs to be flushed after each record.
    /// </summary>
    /// <seealso cref="ICsvPipeWriter{T}.NeedsFlush"/>
    public bool AutoFlush { get; set; }

    /// <summary>
    /// Field count required for each record, if set.
    /// </summary>
    /// <remarks>
    /// Set automatically after the first non-empty record if <see cref="CsvOptions{T}.ValidateFieldCount"/>
    /// is <see langword="true"/>.
    /// </remarks>
    public int? ExpectedFieldCount { get; set; }

    /// <summary>
    /// Inner field writer instance.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected CsvFieldWriter<T> Inner => _inner;

    /// <summary>
    /// Lock for the dematerializer cache, not null if hot reload is active.
    /// </summary>
    private protected readonly ReaderWriterLockSlim? _cacheLock;

    /// <summary>
    /// Dematerializers indexed either by the type (reflection), or the typemap instance (sourcegen).
    /// </summary>
    private readonly Dictionary<object, object> _dematerializerCache = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Previous cache key and value used to avoid re-reading the cache.
    /// </summary>
    private object? _previousKey;

    /// <summary>
    /// Previous cache value used to avoid re-reading the cache.
    /// </summary>
    private object? _previousValue;

    /// <summary>
    /// 0-based index of the current column/field. Reset after each newline.
    /// </summary>
    public int ColumnIndex { get; protected set; }

    /// <summary>
    /// 1-based index of the current line/record. Incremented after each newline.
    /// </summary>
    /// <remarks>
    /// Newlines in quoted fields/strings are not counted, this property represents the logical CSV record index.
    /// </remarks>
    public int LineIndex { get; protected set; }

    /// <summary>
    /// Whether the writer has completed (disposed).
    /// </summary>
    public bool IsCompleted { get; private protected set; }

    private protected readonly CsvFieldWriter<T> _inner;
    private readonly bool _validateFieldCount;

    /// <summary>
    /// Initializes a new writer instance.
    /// </summary>
    /// <param name="inner">Field writer instance to write to</param>
    /// <param name="autoFlush">
    /// Whether to automatically flush after each record if the writer's buffer pressure is high enough.
    /// Automatic flushing is performed in <see cref="CsvWriter{T}.NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </param>
    public CsvAsyncWriter([HandlesResourceDisposal] CsvFieldWriter<T> inner, bool autoFlush)
    {
        Throw.IfDefaultStruct(inner.Writer is null, typeof(CsvFieldWriter<T>));

        _inner = inner;
        _validateFieldCount = inner.Options.ValidateFieldCount;
        AutoFlush = autoFlush;
        LineIndex = 1;

        // omit the overhead of the lock if hot reload is not active
        if (HotReloadService.IsActive)
        {
            _cacheLock = new(LockRecursionPolicy.NoRecursion);
        }

        HotReloadService.RegisterForHotReload(
            this,
            static state =>
            {
                var @this = (CsvWriter<T>)state;

                // completion disposes the readwrite lock
                if (@this.IsCompleted) return;

                using (@this.EnterWrite())
                {
                    @this._previousKey = null;
                    @this._previousValue = null;
                    @this._dematerializerCache.Clear();
                }
            });
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
        ColumnIndex++;
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
        ColumnIndex++;
    }

    /// <summary>
    /// Writes a field with the preceding delimiter if needed.
    /// </summary>
    /// <param name="text">Value to write</param>
    /// <param name="skipEscaping">Whether no escaping should be performed, use with care</param>
    [OverloadResolutionPriority(1)] // prefer writing span instead of generic TField
    public void WriteField(ReadOnlySpan<T> text, bool skipEscaping = false)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteRaw(text, skipEscaping);
        ColumnIndex++;
    }

    /// <inheritdoc cref="WriteField(ReadOnlySpan{T},bool)"/>
    [OverloadResolutionPriority(-1)] // prefer writing T directly if T is char
    public void WriteField(ReadOnlySpan<char> text, bool skipEscaping = false)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteText(text, skipEscaping);
        ColumnIndex++;
    }

    /// <summary>
    /// Writes a sequence of raw characters to the writer.
    /// <see cref="ColumnIndex"/> and <see cref="LineIndex"/> are not tracked automatically, and no escaping is performed.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="columnsWritten">How many columns the value spans</param>
    /// <param name="linesWritten">How many new lines the value spans</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void WriteRaw(ReadOnlySpan<T> value, int columnsWritten = 0, int linesWritten = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnsWritten);
        ArgumentOutOfRangeException.ThrowIfNegative(linesWritten);

        if (!value.IsEmpty)
        {
            Span<T> destination = _inner.Writer.GetSpan(value.Length);
            value.CopyTo(destination);
            _inner.Writer.Advance(value.Length);
        }

        ColumnIndex += columnsWritten;
        LineIndex += linesWritten;
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="AutoFlush"/> is true.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the flush</param>
    /// <exception cref="ObjectDisposedException">The writer has completed</exception>
    public ValueTask NextRecordAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return ValueTask.FromException(new ObjectDisposedException(GetType().Name));

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        ValidateFieldCount();

        _inner.WriteNewline();
        ColumnIndex = 0;
        LineIndex++;

        if (AutoFlush && _inner.Writer.NeedsFlush)
            return _inner.Writer.FlushAsync(cancellationToken);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes the value to the current line using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline,
    /// see <see cref="CsvWriter{T}.NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <param name="value">Value to write</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public void WriteRecord<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().Write(in _inner, value);
    }

    /// <summary>
    /// Writes the value to the current line using the type map.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="CsvWriter{T}.NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <param name="typeMap">Type map to use for writing</param>
    /// <param name="value">Value to write</param>
    public void WriteRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(value);
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).Write(in _inner, value);
    }

    /// <summary>
    /// Writes the header for <typeparamref name="TRecord"/>
    /// to the current line using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="CsvWriter{T}.NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public void WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().WriteHeader(in _inner);
    }

    /// <summary>
    /// Writes the header for <typeparamref name="TRecord"/> to the current line using the type map.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="CsvWriter{T}.NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <param name="typeMap">Type map to use for writing</param>
    public void WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).WriteHeader(in _inner);
    }

    /// <summary>
    /// Completes the writer, flushing any remaining data if <paramref name="exception"/> is null.<br/>
    /// Multiple completions are no-ops.
    /// </summary>
    /// <param name="exception">
    /// Observed exception when writing the data, passed to the inner <see cref="ICsvPipeWriter{T}"/>.
    /// If not null, the final buffer is not flushed and the exception is rethrown.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    public async ValueTask CompleteAsync(Exception? exception = null, CancellationToken cancellationToken = default)
    {
        if (!IsCompleted)
        {
            IsCompleted = true;

            using (_cacheLock)
            {
                HotReloadService.UnregisterForHotReload(this);
                await _inner.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
            }
        }
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
        if (IsCompleted)
            return ValueTask.FromException(new ObjectDisposedException(GetType().Name));

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        return _inner.Writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using the type map,
    /// and pre-increments <see cref="ColumnIndex"/> by <see cref="IDematerializer{T,TValue}.FieldCount"/>.
    /// </summary>
    /// <param name="typeMap">Type map instance</param>
    /// <typeparam name="TRecord">Type to write</typeparam>
    protected IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<TRecord>(
        CsvTypeMap<T, TRecord> typeMap)
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeMap,
            state: typeMap,
            factory: static (options, state) => ((CsvTypeMap<T, TRecord>)state!).GetDematerializer(options));
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using <see cref="CsvOptions{T}.TypeBinder"/>,
    /// and pre-increments <see cref="ColumnIndex"/> by <see cref="IDematerializer{T,TValue}.FieldCount"/>.
    /// </summary>
    /// <typeparam name="TRecord">Type to write</typeparam>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    protected IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<
        [DAM(Messages.ReflectionBound)] TRecord>()
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeof(TRecord),
            state: null,
            factory: static (options, _) => options.TypeBinder.GetDematerializer<TRecord>());
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
        [RequireStaticDelegate] Func<CsvOptions<T>, object?, IDematerializer<T, TRecord>> factory)
    {
        Debug.Assert(cacheKey is Type or CsvTypeMap<T, TRecord>);

        IDematerializer<T, TRecord> dematerializer;
        bool created = false;

        using (EnterRead())
        {
            if (ReferenceEquals(_previousKey, cacheKey))
                return (IDematerializer<T, TRecord>)_previousValue!;

            if (_dematerializerCache.TryGetValue(cacheKey, out object? cached))
            {
                dematerializer = (IDematerializer<T, TRecord>)cached;
            }
            else
            {
                dematerializer = factory(Options, state);
                created = true;
            }
        }

        using (EnterWrite())
        {
            if (created)
            {
                _dematerializerCache[cacheKey] = dematerializer;
            }

            _previousKey = cacheKey;
            _previousValue = dematerializer;
        }

        ColumnIndex += dematerializer.FieldCount;
        return dematerializer;
    }

    /// <summary>
    /// Writes a delimiter if the current column index is not 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void WriteDelimiterIfNeeded()
    {
        if (ColumnIndex > 0) _inner.WriteDelimiter();
    }

    /// <summary>
    /// Validates the current record's field count if <see cref="CsvOptions{T}.ValidateFieldCount"/>
    /// is <see langword="true"/>. Called when moving to the next record.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ValidateFieldCount()
    {
        if (ColumnIndex > 0 && (_validateFieldCount || ExpectedFieldCount.HasValue))
        {
            if (ExpectedFieldCount is null)
            {
                ExpectedFieldCount = ColumnIndex;
            }
            else if (ExpectedFieldCount.GetValueOrDefault() != ColumnIndex)
            {
                ThrowHelper.InvalidFieldCount(LineIndex, ExpectedFieldCount.GetValueOrDefault(), ColumnIndex);
            }
        }
    }

    /// <summary>
    /// Calls <see cref="CompleteAsync"/>.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="CompleteAsync"/> directly is preferable, but multiple completions/disposes are harmless.
    /// </remarks>
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return CompleteAsync();
    }

    /// <summary>Enters the writer lock of <see cref="_cacheLock"/></summary>
    private WriteScope EnterWrite() => new(_cacheLock);

    /// <summary>Enters the reader lock of <see cref="_cacheLock"/></summary>
    private ReadScope EnterRead() => new(_cacheLock);

    private readonly ref struct WriteScope
    {
        private readonly ReaderWriterLockSlim? _rwl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteScope(ReaderWriterLockSlim? rwl)
        {
            (_rwl = rwl)?.EnterWriteLock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _rwl?.ExitWriteLock();
    }

    private readonly ref struct ReadScope
    {
        private readonly ReaderWriterLockSlim? _rwl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadScope(ReaderWriterLockSlim? rwl)
        {
            (_rwl = rwl)?.EnterReadLock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _rwl?.ExitReadLock();
    }
}

/// <summary>
/// Instance that provides convenience methods around <see cref="CsvFieldWriter{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public class CsvWriter<T> : CsvAsyncWriter<T>, IDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Initializes a new writer instance.
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="autoFlush"></param>
    public CsvWriter([HandlesResourceDisposal] CsvFieldWriter<T> inner, bool autoFlush) : base(inner, autoFlush)
    {
        Debug.Assert(inner.Writer is not PipeBufferWriter);
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="CsvAsyncWriter{T}.AutoFlush"/> is true.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has completed</exception>
    public void NextRecord()
    {
        ObjectDisposedException.ThrowIf(IsCompleted, this);

        ValidateFieldCount();

        _inner.WriteNewline();
        ColumnIndex = 0;
        LineIndex++;

        if (AutoFlush && _inner.Writer.NeedsFlush)
            _inner.Writer.Flush();
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

            using (_cacheLock)
            {
                HotReloadService.UnregisterForHotReload(this);
                _inner.Writer.Complete(exception);
            }
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
    /// Calls <see cref="Complete"/>.
    /// </summary>
    /// <remarks>
    /// Calling <see cref="Complete"/> directly is preferable, but multiple completions/disposes are harmless.
    /// </remarks>
    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
        Complete();
    }
}

file static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidFieldCount(int lineIndex, int expected, int actual)
    {
        throw new CsvWriteException($"Invalid field count at line {lineIndex}. Expected {expected}, got {actual}.");
    }
}
