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
public abstract class CsvEnumeratorBase<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The 1-based line index of the current record.
    /// </summary>
    public int Line { get; private set; }

    /// <summary>
    /// The position of the reader in CSV data.
    /// This is the end position of the current record (including possible trailing newline),
    /// or 0 if the enumeration has not started.
    /// </summary>
    public long Position { get; private set; }

    [HandlesResourceDisposal] private readonly CsvParser<T> _parser;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// The CSV parser.
    /// </summary>
    protected CsvParser<T> Parser => _parser;

    /// <summary>
    /// The options used.
    /// </summary>
    public CsvOptions<T> Options => _parser.Options;

    private readonly CsvRecordCallback<T>? _callback;

    /// <summary>
    /// Returns the header record's fields, or empty if none is read.
    /// </summary>
    protected abstract ReadOnlySpan<string> GetHeader();

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
    protected CsvEnumeratorBase(CsvOptions<T> options, ICsvPipeReader<T> reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);
        _parser = CsvParser.Create(options, reader);
        _cancellationToken = cancellationToken;
        _callback = options.RecordCallback;
    }

    private bool CallMoveNextAndIncrementPosition(ref readonly CsvFields<T> fields)
    {
        Line++;
        bool result = false;

        if (_callback is null || !TrySkipRecord(in fields))
        {
            result = MoveNextCore(in fields);
        }

        Position += fields.GetRecordLength(includeTrailingNewline: true);
        return result;
    }

    private bool TrySkipRecord(in CsvFields<T> fields)
    {
        Debug.Assert(_callback is not null);

        ReadOnlySpan<string> header = GetHeader();
        bool skip = false;
        bool headerRead = !header.IsEmpty;

        CsvRecordCallbackArgs<T> args = new(
            in fields,
            header,
            Line,
            Position,
            ref skip,
            ref headerRead);

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
        while (_parser.TryReadLine(out CsvFields<T> line, isFinalBlock: false))
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

        while (_parser.TryReadLine(out CsvFields<T> line, isFinalBlock: false))
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
    /// <param name="fields">Current CSV record</param>
    /// <returns>
    /// <see langword="true"/> if the enumerator produced the next value,
    /// <see langword="false"/> if the record was a header record, or was skipped.
    /// </returns>
    /// <remarks>
    /// When this method is called, <see cref="Position"/> points to the start of the record.
    /// </remarks>
    protected abstract bool MoveNextCore(ref readonly CsvFields<T> fields);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool AdvanceReaderAndMoveNext()
    {
        CsvFields<T> fields;

        while (_parser.TryAdvanceReader())
        {
            while (_parser.TryReadLine(out fields, isFinalBlock: false))
            {
                if (CallMoveNextAndIncrementPosition(in fields)) return true;
            }
        }

        return _parser.TryReadLine(out fields, isFinalBlock: true) && CallMoveNextAndIncrementPosition(in fields);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private protected async ValueTask<bool> AdvanceReaderAndMoveNextAsync()
    {
        CsvFields<T> fields;

        while (await _parser.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
        {
            while (_parser.TryReadLine(out fields, isFinalBlock: false))
            {
                if (CallMoveNextAndIncrementPosition(in fields)) return true;
            }
        }

        return _parser.TryReadLine(out fields, isFinalBlock: true) && CallMoveNextAndIncrementPosition(in fields);
    }

    /// <summary>
    /// Attempts to reset the enumerator to the beginning of the data source.
    /// </summary>
    /// <exception cref="NotSupportedException">The internal reader does not support rewinding</exception>
    protected void ResetCore()
    {
        if (!_parser.TryReset())
        {
            throw new NotSupportedException("The inner data source does not support rewinding");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using (_parser)
        {
            Dispose(true);
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await using (_parser)
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// When overridden, disposes the underlying data source and internal states.
    /// </summary>
    /// <param name="disposing"></param>
    /// <remarks>
    /// The default implementation does nothing. Override both this and <see cref="DisposeAsyncCore"/>
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>
    /// Disposes resources for inheriting classes.
    /// </summary>
    /// <remarks>
    /// The default implementation does nothing. Override both this and <see cref="Dispose(bool)"/>
    /// </remarks>
    protected virtual ValueTask DisposeAsyncCore() => default;
}
