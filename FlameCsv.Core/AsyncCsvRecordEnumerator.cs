using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;

namespace FlameCsv;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style", "IDE0044:Add readonly modifier",
    Justification = "TProcessor is a mutable struct with non-readonly methods")]
internal sealed class AsyncCsvRecordEnumerator<T, TValue, TReader, TProcessor> : IAsyncEnumerator<TValue>
    where T : unmanaged, IEquatable<T>
    where TReader : struct, ICsvPipeReader<T>
    where TProcessor : struct, ICsvProcessor<T, TValue>
{
    private TProcessor _processor;
    private ReadOnlySequence<T> _data;
    private bool _readerCompleted;

    private readonly TReader _reader;
    private readonly CancellationToken _cancellationToken;

    public TValue Current => _current;

    private TValue _current;

    public AsyncCsvRecordEnumerator(
        TReader reader,
        TProcessor processor,
        CancellationToken cancellationToken)
    {
        _reader = reader;
        _processor = processor;
        _cancellationToken = cancellationToken;
        _current = default!;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        if (_processor.TryRead(ref _data, out _current, false))
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
            _reader.AdvanceTo(_data.Start, _data.End);

            (_data, _readerCompleted) = await _reader.ReadAsync(_cancellationToken);

            if (_processor.TryRead(ref _data, out _current, false))
            {
                return true;
            }
        }

        if (_processor.TryRead(ref _data, out _current, true))
        {
            return true;
        }

        _current = default!;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _processor.Dispose();
        await _reader.DisposeAsync();
    }
}
