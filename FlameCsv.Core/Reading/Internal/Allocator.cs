using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal abstract class Allocator<T> : IDisposable where T : unmanaged
{
    protected bool IsDisposed { get; private set; }
    protected abstract MemoryPool<T> Pool { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (length == 0)
        {
            return Memory<T>.Empty;
        }

        Memory<T> memory = MemoryOwner.Memory;

        if (memory.Length < length)
        {
            memory = EnsureCapacity(length, copyOnResize: false);
        }

        return memory.Slice(0, length);
    }

    public Span<T> GetSpan(int length) => GetMemory(length).Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> AsMemory(in ReadOnlySequence<T> sequence)
    {
        return sequence.IsSingleSegment ? sequence.First : GetAsMemoryMultiSegment(sequence);
    }

    private ReadOnlyMemory<T> GetAsMemoryMultiSegment(in ReadOnlySequence<T> sequence)
    {
        int length = checked((int)sequence.Length);
        Memory<T> memory = GetMemory(length);
        sequence.CopyTo(memory.Span);
        return memory;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract ref IMemoryOwner<T> MemoryOwner { get; }

    protected abstract void Dispose(bool disposing);

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected Memory<T> EnsureCapacity(int minimumLength, bool copyOnResize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        ref IMemoryOwner<T> memoryOwner = ref MemoryOwner;
        Memory<T> oldMemory = memoryOwner.Memory;

        if (oldMemory.Length >= minimumLength)
        {
            return oldMemory;
        }

        if (minimumLength > Pool.MaxBufferSize)
        {
            Metrics.TooLargeRent(minimumLength, Pool);
            memoryOwner = HeapMemoryPool<T>.Instance.Rent(minimumLength);
            return memoryOwner.Memory;
        }

        IMemoryOwner<T> newMemoryOwner = Pool.Rent(minimumLength);
        Memory<T> newMemory = newMemoryOwner.Memory;

        if (copyOnResize && !oldMemory.IsEmpty)
        {
            oldMemory.CopyTo(newMemory);
        }

        memoryOwner.Dispose();
        memoryOwner = newMemoryOwner;
        return newMemory;
    }
}

internal sealed class MemoryPoolAllocator<T>(MemoryPool<T> pool) : Allocator<T> where T : unmanaged
{
    protected override MemoryPool<T> Pool => pool;

    private IMemoryOwner<T> _memoryOwner = HeapMemoryOwner<T>.Empty;

    protected override ref IMemoryOwner<T> MemoryOwner => ref _memoryOwner;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MemoryOwner.Dispose();
            MemoryOwner = HeapMemoryOwner<T>.Empty;
        }
    }
}

internal sealed class ThreadLocalAllocator<T>(MemoryPool<T> pool) : Allocator<T> where T : unmanaged
{
    // wrap in a strongbox so we can pass the memoryowner as ref
    private readonly ThreadLocal<StrongBox<IMemoryOwner<T>>> _threadLocal = new(
        () => new StrongBox<IMemoryOwner<T>>(HeapMemoryOwner<T>.Empty),
        trackAllValues: true);

    protected override MemoryPool<T> Pool => pool;

    protected override ref IMemoryOwner<T> MemoryOwner => ref _threadLocal.Value!.Value!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            using (_threadLocal)
            {
                foreach (StrongBox<IMemoryOwner<T>> value in _threadLocal.Values)
                {
                    if (value is not null)
                    {
                        value.Value?.Dispose();
                        value.Value = HeapMemoryOwner<T>.Empty;
                    }
                }
            }
        }
    }
}
