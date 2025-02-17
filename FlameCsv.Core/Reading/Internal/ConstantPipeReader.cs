using System.Buffers;
using JetBrains.Annotations;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Performance optimization for reading from a constant data source to avoid unnecessary copying.
/// </summary>
internal sealed class ConstantPipeReader<T> : ICsvPipeReader<T> where T : unmanaged, IBinaryInteger<T>
{
    private ReadOnlySequence<T> _data;
    private readonly ReadOnlySequence<T> _originalData;
    private readonly IDisposable? _state;
    private readonly bool _leaveOpen;
    private readonly Action<IDisposable?, long>? _onRead;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="ConstantPipeReader{T}"/>.
    /// </summary>
    /// <param name="data">Data retrieved from the source</param>
    /// <param name="state">Data source, e.g., a stream or a text reader</param>
    /// <param name="leaveOpen">Whether not to dispose the state</param>
    /// <param name="onRead">Delegate to advance the data source when reading</param>
    public ConstantPipeReader(
        in ReadOnlySequence<T> data,
        IDisposable? state,
        bool leaveOpen,
        [RequireStaticDelegate] Action<IDisposable?, long>? onRead)
    {
        _state = state;
        _data = _originalData = data;
        _leaveOpen = leaveOpen;
        _onRead = onRead;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConstantPipeReader{T}"/>.
    /// </summary>
    /// <param name="data">CSV data</param>
    public ConstantPipeReader(in ReadOnlySequence<T> data) : this(in data, null, true, null)
    {
    }

    public CsvReadResult<T> Read()
    {
        // simulate reading; advance the data source to keep this perf optimization transparent to the caller
        _onRead?.Invoke(_state, _data.Length);

        return new CsvReadResult<T>(in _data, isCompleted: true);
    }

    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken)
            : new ValueTask<CsvReadResult<T>>(Read());
    }

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        _data = _data.Slice(consumed);
    }

    public bool TryReset()
    {
        if (!_disposed)
        {
            _data = _originalData;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // don't hold on to data after disposing
        _data = default;

        if (!_leaveOpen)
        {
            _state?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return default;

        _disposed = true;

        // don't hold on to data after disposing
        _data = default;

        if (!_leaveOpen)
        {
            if (_state is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            _state?.Dispose();
        }

        return default;
    }
}
