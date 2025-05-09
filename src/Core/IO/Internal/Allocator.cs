using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

internal abstract class Allocator<T> : IDisposable
    where T : unmanaged
{
    protected bool IsDisposed { get; private set; }

    protected abstract Memory<T> GetMemory(int length);

    public Span<T> GetSpan(int length) => GetMemory(length).Span;

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected abstract void Dispose(bool disposing);
}

internal sealed class MemoryPoolAllocator<T>(MemoryPool<T> pool) : Allocator<T>
    where T : unmanaged
{
    private IMemoryOwner<T> _memoryOwner = HeapMemoryOwner<T>.Empty;

    protected override Memory<T> GetMemory(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Memory<T> memory = _memoryOwner.Memory;

        if (memory.Length < length)
        {
            memory = EnsureCapacity(length);
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
    private Memory<T> EnsureCapacity(int minimumLength)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        // fall back to the array-backed shared pool if the requested size is larger than the max buffer size
        var newOwner =
            minimumLength <= pool.MaxBufferSize ? pool.Rent(minimumLength) : MemoryPool<T>.Shared.Rent(minimumLength);

        Memory<T> newMemory = newOwner.Memory;

        _memoryOwner.Dispose();
        _memoryOwner = newOwner;
        return newMemory;
    }
}

internal sealed class StackedAllocator<T>(MemoryPool<T> pool) : Allocator<T>
    where T : unmanaged
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

    protected override Memory<T> GetMemory(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (length == 0)
            return Memory<T>.Empty;

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
            owner = MemoryPool<T>.Shared.Rent(requiredLength);
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
