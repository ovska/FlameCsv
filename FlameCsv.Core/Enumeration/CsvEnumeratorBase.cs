using System.Buffers;
using System.Runtime.CompilerServices;
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
    private readonly ICsvPipeReader<T> _reader;
    private readonly CancellationToken _cancellationToken;
    private bool _readerCompleted;

    /// <summary>
    /// The CSV parser.
    /// </summary>
    protected CsvParser<T> Parser => _parser;

    /// <summary>
    /// The options used.
    /// </summary>
    public CsvOptions<T> Options => _parser.Options;

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
        _parser = CsvParser.Create(options);
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CallMoveNextAndIncrementPosition(ref readonly CsvLine<T> line)
    {
        Line++;
        bool result = MoveNextCore(in line);
        Position += line.GetRecordLength(includeTrailingNewline: true);
        return result;
    }

    /// <inheritdoc cref="System.Collections.IEnumerator.MoveNext"/>
    /// <remarks>
    /// Calling the synchronous method when reading from a <see cref="System.IO.Pipelines.PipeReader"/>
    /// will throw a runtime exception.
    /// </remarks>
    public bool MoveNext()
    {
        while (_parser.TryReadLine(out CsvLine<T> line, isFinalBlock: false))
        {
            if (CallMoveNextAndIncrementPosition(in line))
            {
                return true;
            }
        }

        return ReadFromPipeAndMoveNext();
    }

    /// <inheritdoc cref="IAsyncEnumerator{T}.MoveNextAsync"/>
    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        while (_parser.TryReadLine(out CsvLine<T> line, isFinalBlock: false))
        {
            if (CallMoveNextAndIncrementPosition(in line))
            {
                return new ValueTask<bool>(true);
            }
        }

        return ReadFromPipeAndMoveNextAsync();
    }

    /// <summary>
    /// Attempts to read the next CSV record from the inner parser.
    /// </summary>
    /// <param name="line">Current CSV record</param>
    /// <returns>
    /// <see langword="true"/> if the enumerator produced the next value,
    /// <see langword="false"/> if the record was a header record, or was skipped.
    /// </returns>
    /// <remarks>
    /// When this method is called, <see cref="Position"/> points to the start of the record.
    /// </remarks>
    protected abstract bool MoveNextCore(ref readonly CsvLine<T> line);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool ReadFromPipeAndMoveNext()
    {
        CsvLine<T> line;

        while (!_readerCompleted)
        {
            _parser.AdvanceReader(_reader);
            CsvReadResult<T> result = _reader.Read();
            _parser.SetData(in result.Buffer);
            _readerCompleted = result.IsCompleted;

            while (_parser.TryReadLine(out line, isFinalBlock: false))
            {
                if (CallMoveNextAndIncrementPosition(in line)) return true;
            }
        }

        return _parser.TryReadLine(out line, isFinalBlock: true) && CallMoveNextAndIncrementPosition(in line);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private protected async ValueTask<bool> ReadFromPipeAndMoveNextAsync()
    {
        CsvLine<T> line;

        while (!_readerCompleted)
        {
            _parser.AdvanceReader(_reader);
            CsvReadResult<T> result = await _reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
            _parser.SetData(in result.Buffer);
            _readerCompleted = result.IsCompleted;

            while (_parser.TryReadLine(out line, isFinalBlock: false))
            {
                if (CallMoveNextAndIncrementPosition(in line)) return true;
            }
        }

        return _parser.TryReadLine(out line, isFinalBlock: true) && CallMoveNextAndIncrementPosition(in line);
    }

    /// <summary>
    /// Attempts to reset the enumerator to the beginning of the data source.
    /// </summary>
    /// <exception cref="NotSupportedException">The internal reader does not support rewinding</exception>
    protected void ResetCore()
    {
        if (!_reader.TryReset()) throw new NotSupportedException("The inner data source does not support rewinding");

        // this effectively initializes the parser again
        _parser.SetData(in ReadOnlySequence<T>.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using (_parser)
        using (_reader)
        {
            Dispose(true);
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        using (_parser)
        await using (_reader.ConfigureAwait(false))
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
