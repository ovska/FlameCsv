using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

internal abstract class CsvBufferWriter<T> : ICsvBufferWriter<T> where T : unmanaged
{
    private readonly MemoryPool<T> _allocator;
    private readonly int _bufferSize;
    private readonly int _flushThreshold;
    private readonly bool _leaveOpen;
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

    protected CsvBufferWriter(MemoryPool<T> allocator, in CsvIOOptions options)
    {
        _allocator = allocator ?? MemoryPool<T>.Shared;
        _leaveOpen = options.LeaveOpen;
        _bufferSize = options.BufferSize;
        _flushThreshold = Math.Max(128, (int)(_bufferSize * (31.0 / 32.0)));
        _memoryOwner = _allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        int remaining = Remaining;

        if (remaining < sizeHint || remaining == 0)
        {
            ResizeBuffer(sizeHint);
        }

        Debug.Assert(Remaining >= sizeHint);
        return _buffer.Slice(_unflushed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer(int sizeHint)
    {
        _allocator.EnsureCapacity(
            ref _memoryOwner,
            minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
            copyOnResize: true);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        if ((uint)length > (uint)Remaining)
            Throw.Argument_OutOfRange(nameof(length));

        _unflushed += length;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed <= 0) return default;

        cancellationToken.ThrowIfCancellationRequested();
        ReadOnlyMemory<T> memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;
        return FlushAsyncCore(memory, cancellationToken);
    }

    public void Flush()
    {
        if (_unflushed <= 0) return;

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

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (_unflushed == -1) return;

        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        using (_memoryOwner)
        {
            try
            {
                if (exception is null)
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                exception = CsvWriteException.OnComplete(e);
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<T>.Empty;
                _buffer = default;
                if (!_leaveOpen) await DisposeCoreAsync().ConfigureAwait(false);
            }
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public void Complete(Exception? exception)
    {
        if (_unflushed == -1) return;

        using (_memoryOwner)
        {
            try
            {
                if (exception is null)
                {
                    Flush();
                }
            }
            catch (Exception e)
            {
                exception = CsvWriteException.OnComplete(e);
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<T>.Empty;
                _buffer = default;
                if (!_leaveOpen) DisposeCore();
            }
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    protected abstract void DisposeCore();

    protected virtual ValueTask DisposeCoreAsync()
    {
        try
        {
            DisposeCore();
            return default;
        }
        catch (Exception e)
        {
            return ValueTask.FromException(e);
        }
    }
}
