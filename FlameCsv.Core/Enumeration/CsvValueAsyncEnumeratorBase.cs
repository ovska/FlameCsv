using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// Reads <typeparamref name="TValue"/> records from CSV. Used through <see cref="CsvReader"/>.
/// </summary>
[PublicAPI]
public abstract class CsvValueAsyncEnumeratorBase<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IAsyncEnumerator<TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private bool _readerCompleted;

    private readonly ICsvPipeReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    internal CsvValueAsyncEnumeratorBase(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken)
        : base(options)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        if (TryRead(isFinalBlock: false))
        {
            return new ValueTask<bool>(true);
        }

        return MoveNextAsyncCore();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> MoveNextAsyncCore()
    {
        while (!_readerCompleted)
        {
            _parser.Advance(_reader);

            (ReadOnlySequence<T> sequence, bool readerCompleted) = await _reader
                .ReadAsync(_cancellationToken)
                .ConfigureAwait(false);

            _parser.Reset(in sequence);
            _readerCompleted = readerCompleted;

            if (TryRead(isFinalBlock: false))
            {
                return true;
            }
        }

        return TryRead(isFinalBlock: true);
    }

    /// <summary>
    /// Disposes the underlying data source and internal states, and returns pooled memory.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _reader.DisposeAsync().ConfigureAwait(false);
        base.Dispose(true);
    }

    private protected override void Dispose(bool disposing)
    {
        _reader.Dispose();
        base.Dispose(disposing);
    }
}
