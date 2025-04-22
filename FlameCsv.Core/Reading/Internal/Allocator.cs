using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal abstract class Allocator<T> : IDisposable where T : unmanaged
{
    protected bool IsDisposed { get; private set; }

    public abstract Memory<T> GetMemory(int length);
    public Span<T> GetSpan(int length) => GetMemory(length).Span;

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract void Dispose(bool disposing);
}

internal sealed class PerColumnAllocator<T>(MemoryPool<T> pool) : IDisposable where T : unmanaged
{
    private readonly ConcurrentDictionary<int, MemoryPoolAllocator<T>> _allocators = new();

    public MemoryPoolAllocator<T> this[int index]
        => _allocators.GetOrAdd(
            index,
            static (_, pool) => new MemoryPoolAllocator<T>(pool),
            pool);

    public void Dispose()
    {
        foreach (MemoryPoolAllocator<T> memoryOwner in _allocators.Values)
        {
            memoryOwner.Dispose();
        }

        _allocators.Clear();
    }
}

internal sealed class MemoryPoolAllocator<T>(MemoryPool<T> pool) : Allocator<T> where T : unmanaged
{
    private IMemoryOwner<T> _memoryOwner = HeapMemoryOwner<T>.Empty;

    public override Memory<T> GetMemory(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Memory<T> memory = _memoryOwner.Memory;

        if (memory.Length < length)
        {
            memory = EnsureCapacity(ref _memoryOwner, length, copyOnResize: false);
        }

        return memory.Slice(0, length);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _memoryOwner.Dispose();
            _memoryOwner = HeapMemoryOwner<T>.Empty;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Memory<T> EnsureCapacity(ref IMemoryOwner<T> memoryOwner, int minimumLength, bool copyOnResize)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        Memory<T> oldMemory = memoryOwner.Memory;

        if (oldMemory.Length >= minimumLength)
        {
            return oldMemory;
        }

        if (minimumLength > pool.MaxBufferSize)
        {
            Metrics.TooLargeRent(minimumLength, pool);
            memoryOwner = HeapMemoryPool<T>.Instance.Rent(minimumLength);
            return memoryOwner.Memory;
        }

        IMemoryOwner<T> newMemoryOwner = pool.Rent(minimumLength);
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

internal sealed class SlabAllocator<T>(MemoryPool<T> pool) : Allocator<T> where T : unmanaged
{
    public void Reset()
    {
        foreach (var entry in _queue)
        {
            lock (entry)
            {
                entry.Remaining = entry.MemoryOwner.Memory;
            }
        }
    }

    private readonly ConcurrentQueue<Entry> _queue = [];

    public override Memory<T> GetMemory(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (length == 0) return Memory<T>.Empty;

        foreach (var entry in _queue)
        {
            if (entry.Remaining.Length >= length)
            {
                lock (entry)
                {
                    if (entry.Remaining.Length >= length)
                    {
                        Memory<T> memory = entry.Remaining.Slice(0, length);
                        entry.Remaining = entry.Remaining.Slice(length);
                        return memory;
                    }
                }
            }
        }

        _queue.Enqueue(Allocate(length, out Memory<T> allocatedMemory));
        Debug.Assert(allocatedMemory.Length == length, $"Expected {length} but got {allocatedMemory.Length}");
        return allocatedMemory;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            while (_queue.TryDequeue(out Entry? entry))
            {
                entry.MemoryOwner.Dispose();
                entry.MemoryOwner = HeapMemoryOwner<T>.Empty;
                entry.Remaining = Memory<T>.Empty;
            }
        }
    }

    private Entry Allocate(int requiredLength, out Memory<T> memory)
    {
        IMemoryOwner<T> owner;

        if (requiredLength > pool.MaxBufferSize)
        {
            Metrics.TooLargeRent(requiredLength, pool);
            owner = HeapMemoryPool<T>.Instance.Rent(requiredLength);
        }
        else
        {
            int toRent = requiredLength;
            if (toRent < 4096)
            {
                toRent = Math.Min(4096, pool.MaxBufferSize);
            }

            owner = pool.Rent(toRent);
        }

        Memory<T> ownedMemory = owner.Memory;
        memory = ownedMemory.Slice(0, requiredLength);

        return new Entry { MemoryOwner = owner, Remaining = ownedMemory.Slice(requiredLength) };
    }

    private sealed class Entry
    {
        public required IMemoryOwner<T> MemoryOwner { get; set; }
        public required Memory<T> Remaining { get; set; }
    }
}
