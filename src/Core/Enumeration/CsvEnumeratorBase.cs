using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Base class for synchronous enumerators.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public abstract class CsvEnumeratorBase<T> : IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns the 1-based line index of the current record.
    /// </summary>
    public int Line => _reader._recordBuffer.LineNumber;

    [HandlesResourceDisposal]
    private protected readonly CsvReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// The CSV parser.
    /// </summary>
    internal protected CsvReader<T> Reader => _reader;

    /// <summary>
    /// The options used.
    /// </summary>
    public CsvOptions<T> Options => _reader.Options;

    private readonly CsvRecordCallback<T>? _callback;

    /// <summary>
    /// Returns the header record's fields, or empty if none is read.
    /// </summary>
    protected abstract ImmutableArray<string> GetHeader();

    /// <summary>
    /// Resets the header. No-op if the header is not read.
    /// </summary>
    protected abstract void ResetHeader();

    /// <summary>
    /// Creates a new instance of the enumerator.
    /// </summary>
    /// <param name="options">Options to use for reading</param>
    /// <param name="reader">Data source</param>
    /// <param name="cancellationToken">Token to cancel asynchronous enumeration</param>
    protected CsvEnumeratorBase(CsvOptions<T> options, ICsvBufferReader<T> reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);
        _reader = new CsvReader<T>(options, reader);
        _cancellationToken = cancellationToken;
        _callback = options.RecordCallback;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TrySkipOrMoveNext(RecordView view)
    {
        bool result = false;

        if (_callback is null || !TrySkipRecord(view))
        {
            result = MoveNextCore(view);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySkipRecord(RecordView view)
    {
        Check.NotNull(_callback);

        ReadOnlySpan<string> header = GetHeader().AsSpan();
        bool skip = false;
        bool headerRead = !header.IsEmpty;

        CsvRecordCallbackArgs<T> args = new(new CsvRecordRef<T>(_reader, view), header, ref skip, ref headerRead);
        _callback(in args);

        if (!headerRead && !header.IsEmpty)
        {
            ResetHeader();
        }

        return skip;
    }

    /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_callback is null && _reader._recordBuffer.TryPop(out RecordView view) && MoveNextCore(view))
        {
            return true;
        }

        return MoveNextSlow();
    }

    /// <inheritdoc cref="IAsyncEnumerator{T}.MoveNextAsync"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> MoveNextAsync()
    {
        if (
            _callback is null
            && !_cancellationToken.IsCancellationRequested
            && _reader._recordBuffer.TryPop(out RecordView view)
            && MoveNextCore(view)
        )
        {
            return new ValueTask<bool>(true);
        }

        return MoveNextSlowAsync();
    }

    /// <summary>
    /// Attempts to read the next CSV record from the inner parser.
    /// </summary>
    /// <param name="record">Current CSV record</param>
    /// <returns>
    /// <c>true</c> if the enumerator produced the next value,
    /// <c>false</c> if the record was a header record, or was skipped.
    /// </returns>
    internal abstract bool MoveNextCore(RecordView record);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool MoveNextSlow()
    {
        RecordView view;

        while (_reader.TryReadLine(out view))
        {
            if (TrySkipOrMoveNext(view))
            {
                return true;
            }
        }

        while (_reader.TryAdvanceReader())
        {
            while (_reader.TryReadLine(out view))
            {
                if (TrySkipOrMoveNext(view))
                    return true;
            }
        }

        return _reader.TryReadLine(out view) && TrySkipOrMoveNext(view);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))] // TODO PERF: profile
    private protected async ValueTask<bool> MoveNextSlowAsync()
    {
        RecordView view;

        while (_reader.TryReadLine(out view))
        {
            if (TrySkipOrMoveNext(view))
                return true;
        }

        while (await _reader.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
        {
            while (_reader.TryReadLine(out view))
            {
                if (TrySkipOrMoveNext(view))
                    return true;
            }
        }

        return _reader.TryReadLine(out view) && TrySkipOrMoveNext(view);
    }

    /// <summary>
    /// Attempts to reset the enumerator to the beginning of the data source.
    /// </summary>
    /// <exception cref="NotSupportedException">The internal reader does not support rewinding</exception>
    protected void ResetCore()
    {
        _reader.Reset();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        using (_reader)
        {
            Dispose(true);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await using (_reader.ConfigureAwait(false))
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// When overridden, disposes the underlying data source and internal states.
    /// </summary>
    /// <param name="disposing"></param>
    /// <remarks>
    /// The default implementation does nothing. Override both this and <see cref="DisposeAsyncCore"/>
    /// </remarks>
    protected virtual void Dispose(bool disposing) { }

    /// <summary>
    /// Disposes resources for inheriting classes.
    /// </summary>
    /// <remarks>
    /// The default implementation does nothing. Override both this and <see cref="Dispose(bool)"/>
    /// </remarks>
    protected virtual ValueTask DisposeAsyncCore() => default;
}
