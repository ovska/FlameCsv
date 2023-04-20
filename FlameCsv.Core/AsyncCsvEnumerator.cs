using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// Asynchronously enumerates CSV records.
/// </summary>
/// <remarks>
/// If the CSV has a header record, it will be processed first before any records are yielded.
/// </remarks>
/// <typeparam name="T"></typeparam>
public sealed class AsyncCsvEnumerator<T> : CsvEnumeratorBase<T>, IAsyncEnumerator<CsvRecord<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly ICsvPipeReader<T> _reader;

    /// <summary>Current unadvanced buffer from reader, or empty before first call to ReadAsync</summary>
    private ReadOnlySequence<T> _data;

    /// <summary>Last call to ReadAsync returned IsCompleted = true</summary>
    private bool _readerCompleted;

    internal AsyncCsvEnumerator(
        ICsvPipeReader<T> reader,
        CsvReaderOptions<T> options,
        CancellationToken cancellationToken)
        : base(options, cancellationToken)
    {
        _reader = reader;
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

            (_data, _readerCompleted) = await _reader.ReadAsync(_cancellationToken);

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
            Position += Current.Data.Length;
            Current = default;
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose(true);
        await _reader.DisposeAsync();
    }
}
