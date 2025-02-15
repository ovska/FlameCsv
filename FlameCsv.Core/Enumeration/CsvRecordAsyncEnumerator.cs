using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
[PublicAPI]
public sealed class CsvRecordAsyncEnumerator<T> : CsvRecordEnumeratorBase<T>, IAsyncEnumerator<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    /// <summary>Last call to ReadAsync returned IsCompleted = true</summary>
    private bool _readerCompleted;

    internal CsvRecordAsyncEnumerator(
        ICsvPipeReader<T> reader,
        CsvOptions<T> options,
        CancellationToken cancellationToken)
        : base(options)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        if (MoveNextCore())
        {
            return new ValueTask<bool>(true);
        }

        return ReadAndMoveNextAsync();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> ReadAndMoveNextAsync()
    {
        while (!_readerCompleted)
        {
            _parser.Advance(_reader);

            (ReadOnlySequence<T> sequence, bool completed) = await _reader
                .ReadAsync(_cancellationToken)
                .ConfigureAwait(false);

            _parser.Reset(in sequence);
            _readerCompleted = completed;

            if (MoveNextCore())
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MoveNextCore()
    {
        if (MoveNextCore(isFinalBlock: false))
        {
            return true;
        }

        if (_readerCompleted)
        {
            if (MoveNextCore(isFinalBlock: true))
            {
                return true;
            }

            _current = default;
        }

        return false;
    }

    /// <summary>Disposes the undlerying data source.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            await using (_reader.ConfigureAwait(false))
            {
                base.Dispose(true);
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            using (_reader)
            {
                base.Dispose(disposing);
            }
        }
    }
}
