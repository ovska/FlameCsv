using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

internal abstract class CsvBufferWriter<T> : ICsvBufferWriter<T>
    where T : unmanaged
{
    private readonly MemoryPool<T> _allocator;
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

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= _flushThreshold;
    }

    protected bool IsDisposed => _unflushed == -1;

    protected CsvBufferWriter(MemoryPool<T> allocator, in CsvIOOptions options)
    {
        _allocator = allocator ?? MemoryPool<T>.Shared;
        _bufferSize = options.BufferSize;
        _flushThreshold = (int)(_bufferSize * (31d / 32d)); // default 512 bytes
        _memoryOwner = _allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        Memory<T> memory = _buffer.Slice(_unflushed);

        if (memory.Length < sizeHint || memory.Length == 0)
        {
            return ResizeBuffer(sizeHint);
        }

        Debug.Assert(memory.Length >= sizeHint);
        return memory;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Memory<T> ResizeBuffer(int sizeHint)
    {
        _allocator.EnsureCapacity(
            ref _memoryOwner,
            minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
            copyOnResize: true
        );
        _buffer = _memoryOwner.Memory;
        return _buffer.Slice(_unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)Remaining, nameof(length));
        _unflushed += length;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed <= 0)
            return default;

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        ReadOnlyMemory<T> memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;
        return FlushAsyncCore(memory, cancellationToken);
    }

    public void Flush()
    {
        if (_unflushed <= 0)
            return;

        ReadOnlyMemory<T> memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;
        FlushCore(memory);
    }

    /// <summary>
    /// Flushes the buffer to the underlying stream.
    /// </summary>
    /// <returns>
    /// Number of characters flushed from the buffer.
    /// </returns>
    protected abstract void FlushCore(ReadOnlyMemory<T> memory);

    /// <inheritdoc cref="FlushCore"/>
    protected abstract ValueTask FlushAsyncCore(ReadOnlyMemory<T> memory, CancellationToken cancellationToken);

    public async ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
            return;

        using (_memoryOwner)
        {
            try
            {
                if (exception is null && !cancellationToken.IsCancellationRequested)
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<T>.Empty;
                _buffer = default;
                await DisposeCoreAsync().ConfigureAwait(false);
            }
        }
    }

    public void Complete(Exception? exception)
    {
        if (IsDisposed)
            return;

        using (_memoryOwner)
        {
            try
            {
                if (exception is null)
                {
                    Flush();
                }
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<T>.Empty;
                _buffer = default;
                DisposeCore();
            }
        }
    }

    protected abstract void DisposeCore();
    protected abstract ValueTask DisposeCoreAsync();
}
