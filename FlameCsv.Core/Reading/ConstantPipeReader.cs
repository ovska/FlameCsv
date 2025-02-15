using System.Buffers;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Performance optimization for reading from a constant data source to avoid unnecessary copying.
/// </summary>
/// <seealso cref="CsvReader.CreatePipeReader(System.IO.Stream,System.Buffers.MemoryPool{byte},bool)"/>
/// <seealso cref="CsvReader.CreatePipeReader(System.IO.TextReader,System.Buffers.MemoryPool{char},int)"/>
internal sealed class ConstantPipeReader<T> : ICsvPipeReader<T> where T : unmanaged, IBinaryInteger<T>
{
    private ReadOnlySequence<T> _data;
    private readonly IDisposable _state;
    private readonly bool _leaveOpen;
    private readonly Action<IDisposable, long> _onRead;

    /// <summary>
    /// Initializes a new instance of <see cref="ConstantPipeReader{T}"/>.
    /// </summary>
    /// <param name="data">Data retrieved from the source</param>
    /// <param name="state">Data source, e.g., a stream or a text reader</param>
    /// <param name="leaveOpen">Whether not to dispose the state</param>
    /// <param name="onRead">Delegate to advance the data source when reading</param>
    public ConstantPipeReader(
        ReadOnlyMemory<T> data,
        IDisposable state,
        bool leaveOpen,
        [RequireStaticDelegate] Action<IDisposable, long> onRead)
    {
        _state = state;
        _data = new(data);
        _leaveOpen = leaveOpen;
        _onRead = onRead;
    }

    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken);
        }

        // simulate reading; advance the data source to keep this perf optimization transparent to the caller
        _onRead.Invoke(_state, _data.Length);

        return new ValueTask<CsvReadResult<T>>(new CsvReadResult<T>(in _data, isCompleted: true));
    }

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        _data = _data.Slice(consumed);
    }

    public void Dispose()
    {
        _data = default;

        if (!_leaveOpen)
        {
            _state.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        _data = default;

        if (!_leaveOpen)
        {
            if (_state is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            _state.Dispose();
        }

        return default;
    }
}
