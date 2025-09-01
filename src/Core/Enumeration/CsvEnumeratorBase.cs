using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.IO;
using FlameCsv.Reading;
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
    /// The 1-based line index of the current record.
    /// </summary>
    public int Line { get; protected set; }

    /// <summary>
    /// The position of the reader in CSV data.
    /// This is the end position of the current record (including possible trailing newline),
    /// or 0 if the enumeration has not started.
    /// </summary>
    public long Position => Math.Min(_position, _reader._reader.Position);

    private long _position;

    [HandlesResourceDisposal]
    private readonly CsvReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// The CSV parser.
    /// </summary>
    protected CsvReader<T> Reader => _reader;

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
    private bool CallMoveNextAndIncrementPosition(ref readonly CsvSlice<T> record)
    {
        Line++;
        bool result = false;

        if (_callback is null || !TrySkipRecord(in record))
        {
            result = MoveNextCore(in record);
        }

        _position += record.Record.GetLength(includeTrailingNewline: true);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySkipRecord(in CsvSlice<T> slice)
    {
        Debug.Assert(_callback is not null);

        ReadOnlySpan<string> header = GetHeader().AsSpan();
        bool skip = false;
        bool headerRead = !header.IsEmpty;

        CsvRecordCallbackArgs<T> args = new(
            new CsvRecordRef<T>(in slice),
            header,
            Line,
            Position,
            ref skip,
            ref headerRead
        );

        _callback(in args);

        if (!headerRead && !header.IsEmpty)
        {
            ResetHeader();
        }

        return skip;
    }

    /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
    public bool MoveNext()
    {
        while (_reader.TryReadLine(out CsvSlice<T> line))
        {
            if (CallMoveNextAndIncrementPosition(in line))
            {
                return true;
            }
        }

        return AdvanceReaderAndMoveNext();
    }

    /// <inheritdoc cref="IAsyncEnumerator{T}.MoveNextAsync"/>
    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        while (_reader.TryReadLine(out CsvSlice<T> line))
        {
            if (CallMoveNextAndIncrementPosition(in line))
            {
                return new ValueTask<bool>(true);
            }
        }

        return AdvanceReaderAndMoveNextAsync();
    }

    /// <summary>
    /// Attempts to read the next CSV record from the inner parser.
    /// </summary>
    /// <param name="slice">Current CSV record</param>
    /// <returns>
    /// <c>true</c> if the enumerator produced the next value,
    /// <c>false</c> if the record was a header record, or was skipped.
    /// </returns>
    /// <remarks>
    /// When this method is called, <see cref="Position"/> points to the start of the record.
    /// </remarks>
    internal abstract bool MoveNextCore(ref readonly CsvSlice<T> slice);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool AdvanceReaderAndMoveNext()
    {
        CsvSlice<T> slice;

        while (_reader.TryAdvanceReader())
        {
            while (_reader.TryReadLine(out slice))
            {
                if (CallMoveNextAndIncrementPosition(in slice))
                    return true;
            }
        }

        return _reader.TryReadLine(out slice) && CallMoveNextAndIncrementPosition(in slice);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))] // TODO PERF: profile
    private protected async ValueTask<bool> AdvanceReaderAndMoveNextAsync()
    {
        CsvSlice<T> slice;

        while (await _reader.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
        {
            while (_reader.TryReadLine(out slice))
            {
                if (CallMoveNextAndIncrementPosition(in slice))
                    return true;
            }
        }

        return _reader.TryReadLine(out slice) && CallMoveNextAndIncrementPosition(in slice);
    }

    /// <summary>
    /// Attempts to reset the enumerator to the beginning of the data source.
    /// </summary>
    /// <exception cref="NotSupportedException">The internal reader does not support rewinding</exception>
    protected void ResetCore()
    {
        _reader.Reset();
        Line = 0;
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
