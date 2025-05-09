using JetBrains.Annotations;

namespace FlameCsv.IO.Internal;

/// <summary>
/// Performance optimization for reading from a constant data source to avoid unnecessary copying.
/// </summary>
internal sealed class ConstantBufferReader<T> : ICsvBufferReader<T>
    where T : unmanaged
{
    private ReadOnlyMemory<T> _data;
    private ReadOnlyMemory<T> _originalData;
    private readonly IDisposable? _state;
    private readonly bool _leaveOpen;
    private readonly Action<IDisposable?, int>? _onRead;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="ConstantBufferReader{T}"/>.
    /// </summary>
    /// <param name="data">Data retrieved from the source</param>
    /// <param name="leaveOpen">Whether not to dispose the state</param>
    /// <param name="state">Data source, e.g., a stream or a text reader</param>
    /// <param name="onRead">Delegate to advance the data source when reading</param>
    public ConstantBufferReader(
        ReadOnlyMemory<T> data,
        bool leaveOpen = false,
        IDisposable? state = null,
        [RequireStaticDelegate] Action<IDisposable?, int>? onRead = null
    )
    {
        _state = state;
        _data = _originalData = data;
        _leaveOpen = leaveOpen;
        _onRead = onRead;
    }

    public CsvReadResult<T> Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // simulate reading; advance the data source to keep this perf optimization transparent to the caller
        _onRead?.Invoke(_state, _data.Length);

        return new CsvReadResult<T>(_data, isCompleted: true);
    }

    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken);
        }

        return new ValueTask<CsvReadResult<T>>(Read());
    }

    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)_data.Length, nameof(count));
        _data = _data.Slice(count);
    }

    public bool TryReset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _data = _originalData;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // don't hold on to data after disposing
        _data = default;
        _originalData = default;

        if (!_leaveOpen)
        {
            _state?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return default;

        _disposed = true;

        // don't hold on to data after disposing
        _data = default;
        _originalData = default;

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
