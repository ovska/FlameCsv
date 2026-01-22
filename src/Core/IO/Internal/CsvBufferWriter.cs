using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

#pragma warning disable RCS1229 // Use async/await when necessary

internal abstract class CsvBufferWriter<T> : ICsvBufferWriter<T>
    where T : unmanaged
{
    public IBufferPool BufferPool { get; }

    private readonly int _bufferSize;
    private readonly int _flushThreshold;
    private int _unflushed;
    private Memory<T> _buffer;
    private IMemoryOwner<T> _memoryOwner;

    public int Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length - _unflushed;
    }

    public bool NeedsDrain
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= _flushThreshold;
    }

    internal ReadOnlyMemory<T> WrittenMemory => _buffer.Slice(0, _unflushed);

    protected bool IsDisposed => _unflushed == -1;

    protected CsvBufferWriter(in CsvIOOptions options)
    {
        BufferPool = options.EffectiveBufferPool;
        _bufferSize = options.BufferSize;
        _flushThreshold = (int)(_bufferSize * (31d / 32d)); // default 512 bytes
        _memoryOwner = BufferPool.Rent<T>(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        if (sizeHint <= 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            sizeHint = 1;
        }

        if (_buffer.Length - _unflushed < sizeHint)
        {
            _buffer = BufferPool.EnsureCapacity(
                ref _memoryOwner,
                minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
                copyOnResize: true
            );
        }

        Memory<T> memory = _buffer.Slice(_unflushed);
        Check.GreaterThanOrEqual(memory.Length, sizeHint);
        return memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)Remaining, nameof(length));
        _unflushed += length;
    }

    public void Drain()
    {
        if (_unflushed <= 0)
            return;

        ReadOnlyMemory<T> memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;
        DrainCore(memory);
    }

    public ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed <= 0)
            return default;

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        ReadOnlyMemory<T> memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;
        return DrainAsyncCore(memory, cancellationToken);
    }

    /// <summary>
    /// Drains the buffer to the underlying stream.
    /// </summary>
    protected abstract void DrainCore(ReadOnlyMemory<T> memory);

    /// <summary>
    /// Flushes the underlying stream.
    /// </summary>
    protected abstract void FlushCore();

    /// <inheritdoc cref="DrainCore"/>
    protected virtual ValueTask DrainAsyncCore(ReadOnlyMemory<T> memory, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        try
        {
            DrainCore(memory);
            return default;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    /// <inheritdoc cref="FlushCore"/>
    protected virtual ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        try
        {
            FlushCore();
            return default;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public async ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
            return;

        try
        {
            if (exception is null && !cancellationToken.IsCancellationRequested && _unflushed > 0)
            {
                await DrainAsyncCore(_buffer.Slice(0, _unflushed), cancellationToken).ConfigureAwait(false);
                await FlushAsyncCore(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _unflushed = -1;
            _memoryOwner.Dispose();
            _memoryOwner = null!;
            _buffer = default;
            await DisposeCoreAsync().ConfigureAwait(false);
        }
    }

    public void Complete(Exception? exception)
    {
        if (IsDisposed)
            return;

        try
        {
            if (exception is null && _unflushed > 0)
            {
                DrainCore(_buffer.Slice(0, _unflushed));
                FlushCore();
            }
        }
        finally
        {
            _unflushed = -1;
            _memoryOwner.Dispose();
            _memoryOwner = null!;
            _buffer = default;
            DisposeCore();
        }
    }

    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeCoreAsync();
}
