using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Extensions;

internal static class MemoryPoolExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Memory<T> EnsureCapacity<T>(
        this MemoryPool<T> pool,
        [AllowNull] ref IMemoryOwner<T> memoryOwner,
        int minimumLength,
        bool copyOnResize = false
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        if (minimumLength == 0)
            return Memory<T>.Empty;

        Memory<T> currentMemory = memoryOwner?.Memory ?? Memory<T>.Empty;

        if (currentMemory.Length >= minimumLength)
        {
            return currentMemory;
        }

        if (minimumLength > pool.MaxBufferSize)
        {
            pool = MemoryPool<T>.Shared;
        }

        if (memoryOwner is null)
        {
            return (memoryOwner = pool.Rent(minimumLength)).Memory;
        }

        IMemoryOwner<T> newMemory = pool.Rent(minimumLength);

        if (copyOnResize)
        {
            currentMemory.CopyTo(newMemory.Memory);
        }

        memoryOwner.Dispose();
        memoryOwner = newMemory;
        return newMemory.Memory;
    }
}

internal class HeapMemoryOwner<T>(T[] array) : IMemoryOwner<T>
{
#if DEBUG
    private bool _disposed;
#endif

    public static HeapMemoryOwner<T> Empty { get; } = new([]);

    public Memory<T> Memory
    {
        get
        {
#if DEBUG
            ObjectDisposedException.ThrowIf(_disposed, this);
#endif

            return array;
        }
    }

    public void Dispose()
    {
#if DEBUG
        // Don't dispose the empty instance
        if (array.Length != 0)
            _disposed = true;
#endif
    }
}

internal class HeapMemoryPool<T> : MemoryPool<T>
{
    public static HeapMemoryPool<T> Instance { get; } = new();

    [Obsolete("Use HeapMemoryPool<T>.Instance instead", true)]
    public static new MemoryPool<T> Shared => throw new UnreachableException();

    public override int MaxBufferSize => Array.MaxLength;

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize == 0)
        {
            return HeapMemoryOwner<T>.Empty;
        }

        if (minBufferSize == -1)
        {
            minBufferSize = 4096;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);

        T[] array = GC.AllocateUninitializedArray<T>((int)BitOperations.RoundUpToPowerOf2((uint)minBufferSize));
        return new HeapMemoryOwner<T>(array);
    }

    protected override void Dispose(bool disposing)
    {
        // no-op
    }
}
