using System.ComponentModel;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Instance that provides convenience methods around <see cref="CsvFieldWriter{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public class CsvWriter<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Options instance of this writer.
    /// </summary>
    public CsvOptions<T> Options { get; }

    /// <summary>
    /// Whether to automatically check if the writer needs to be flushed after each record.
    /// </summary>
    /// <seealso cref="ICsvBufferWriter{T}.NeedsFlush"/>
    public bool AutoFlush { get; set; }

    /// <summary>
    /// Inner field writer instance.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected CsvFieldWriter<T> Inner => _inner;

    /// <summary>
    /// Lock for the dematerializer cache, not null if hot reload is active.
    /// </summary>
    private readonly ReaderWriterLockSlim? _cacheLock;

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
    public int ColumnIndex { get; private set; }

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
    protected bool IsCompleted { get; private set; }

    private readonly CsvFieldWriter<T> _inner;

    public CsvWriter(CsvOptions<T> options, CsvFieldWriter<T> inner, bool autoFlush)
    {
        ArgumentNullException.ThrowIfNull(options);
        Throw.IfDefaultStruct(inner.Writer is null, typeof(CsvFieldWriter<T>));

        options.MakeReadOnly();

        _inner = inner;
        Options = options;
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
    /// <param name="value">Value to write</param>
    /// <typeparam name="TField">Field type that will be converted</typeparam>
    public void WriteField<TField>(TField? value)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteField(Options.GetConverter<TField?>(), value);
        ColumnIndex++;
    }

    /// <summary>
    /// Writes a field with the preceding delimiter if needed.
    /// </summary>
    /// <param name="text">Value to write</param>
    /// <param name="skipEscaping">Whether no escaping should be performed, use with care</param>
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
    /// Writes a sequence of raw characters to the writer. <see cref="ColumnIndex"/> and <see cref="LineIndex"/>
    /// are not tracked automatically, and no escaping is performed.
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
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="AutoFlush"/> is true.
    /// </summary>
    /// <remarks>
    /// Using the synchronous overload when writing to a <see cref="System.IO.Pipelines.PipeWriter"/>
    /// and <see cref="AutoFlush"/> is true will throw a runtime exception.
    /// </remarks>
    public void NextRecord()
    {
        _inner.WriteNewline();
        ColumnIndex = 0;
        LineIndex++;
        FlushIfNeeded();
    }

    /// <summary>
    /// Writes a newline and flushes the buffer if needed when <see cref="AutoFlush"/> is true.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the flush</param>
    public ValueTask NextRecordAsync(CancellationToken cancellationToken = default)
    {
        _inner.WriteNewline();
        ColumnIndex = 0;
        LineIndex++;
        return FlushIfNeededAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the value to the current line using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.<br/>
    /// </remarks>
    /// <param name="value">Value to write</param>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
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
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.
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
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public void WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().WriteHeader(in _inner);
    }

    /// <summary>
    /// Writes the header for <typeparamref name="TRecord"/> to the current line using the type map.
    /// </summary>
    /// <remarks>
    /// Does not write a trailing newline, see <see cref="NextRecord"/> and <see cref="NextRecordAsync"/>.
    /// </remarks>
    /// <param name="typeMap">Type map to use for writing</param>
    public void WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).WriteHeader(in _inner);
    }

    /// <summary>
    /// Completes the writer, flushing any remaining data if <paramref name="exception"/> is null.
    /// </summary>
    /// <param name="exception">
    /// Observed exception when writing the data.
    /// If not null, the final buffer is not flushed and the exception is rethrown.
    /// </param>
    /// <remarks>
    /// Using the synchronous overload when writing to a <see cref="System.IO.Pipelines.PipeWriter"/>
    /// will throw a runtime exception.
    /// </remarks>
    public void Complete(Exception? exception = null)
    {
        if (!IsCompleted)
        {
            using (_cacheLock)
            {
                IsCompleted = true;
                _inner.Writer.Complete(exception);
            }
        }
    }

    /// <summary>
    /// Completes the writer, flushing any remaining data if <paramref name="exception"/> is null.
    /// </summary>
    /// <param name="exception">
    /// Observed exception when writing the data.
    /// If not null, the final buffer is not flushed and the exception is rethrown.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    public async ValueTask CompleteAsync(Exception? exception = null, CancellationToken cancellationToken = default)
    {
        if (!IsCompleted)
        {
            using (_cacheLock)
            {
                IsCompleted = true;
                await _inner.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    /// <remarks>
    /// Using the synchronous overload when writing to a <see cref="System.IO.Pipelines.PipeWriter"/>
    /// will throw a runtime exception.
    /// </remarks>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(IsCompleted, this);
        _inner.Writer.Flush();
    }

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the write operation</param>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return ValueTask.FromException(new ObjectDisposedException(GetType().Name));

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        return _inner.Writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using the type map.
    /// </summary>
    /// <param name="typeMap">Type map instance</param>
    protected IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<TRecord>(
        CsvTypeMap<T, TRecord> typeMap)
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeMap,
            state: typeMap,
            factory: static (options, state) => ((CsvTypeMap<T, TRecord>)state!).GetDematerializer(options));
    }

    /// <summary>
    /// Returns or creates a cached dematerializer using <see cref="CsvOptions{T}.TypeBinder"/>.
    /// </summary>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    protected IDematerializer<T, TRecord>
        GetDematerializerAndIncrementFieldCount<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        return GetDematerializerAndIncrementFieldCountCore(
            cacheKey: typeof(TRecord),
            state: null,
            factory: static (options, _) => options.TypeBinder.GetDematerializer<TRecord>());
    }

    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCountCore<TRecord>(
        object cacheKey,
        object? state,
        [RequireStaticDelegate] Func<CsvOptions<T>, object?, IDematerializer<T, TRecord>> factory)
    {
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
    /// Flushes if <see cref="AutoFlush"/> and <see cref="ICsvBufferWriter{T}.NeedsFlush"/> are true.
    /// </summary>
    /// <remarks>
    /// Using the synchronous overload when writing to a <see cref="System.IO.Pipelines.PipeWriter"/>
    /// will throw a runtime exception.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void FlushIfNeeded()
    {
        ObjectDisposedException.ThrowIf(IsCompleted, this);

        if (AutoFlush && _inner.Writer.NeedsFlush)
            _inner.Writer.Flush();
    }

    /// <summary>
    /// Flushes if <see cref="AutoFlush"/> and <see cref="ICsvBufferWriter{T}.NeedsFlush"/> are true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask FlushIfNeededAsync(CancellationToken cancellationToken)
    {
        if (IsCompleted)
            return ValueTask.FromException(new ObjectDisposedException(GetType().Name));

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        if (AutoFlush && _inner.Writer.NeedsFlush)
            return _inner.Writer.FlushAsync(cancellationToken);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
        Complete();
    }

    /// <inheritdoc />
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
            rwl?.EnterWriteLock();
            _rwl = rwl;
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
            rwl?.EnterReadLock();
            _rwl = rwl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _rwl?.ExitReadLock();
    }
}
