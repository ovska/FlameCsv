using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
public sealed class CsvRecordAsyncEnumerator<T> : CsvRecordEnumeratorBase<T>, IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    /// <summary>Current unadvanced buffer from reader, or empty before first call to ReadAsync</summary>
    private ReadOnlySequence<T> _data;

    /// <summary>Last call to ReadAsync returned IsCompleted = true</summary>
    private bool _readerCompleted;

    internal CsvRecordAsyncEnumerator(
        ICsvPipeReader<T> reader,
        in CsvReadingContext<T> context,
        CancellationToken cancellationToken)
        : base(in context)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

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
            _reader.AdvanceTo(_data.Start, _data.End);

            (_data, _readerCompleted) = await _reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

            if (MoveNextCore())
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MoveNextCore()
    {
        if (MoveNextCore(ref _data, isFinalBlock: false))
        {
            return true;
        }

        if (_readerCompleted)
        {
            if (MoveNextCore(ref _data, isFinalBlock: true))
            {
                return true;
            }

            // reached end of data
            Position += _current.RawRecord.Length;
            _current = default;
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Dispose(true);
            await _reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Ensure arrays always get returned to the pool
        if (!_disposed)
        {
            base.Dispose(disposing);
            _reader.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
