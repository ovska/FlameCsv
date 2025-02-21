using System.Buffers;
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
        bool copyOnResize = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        if (minimumLength == 0)
            return Memory<T>.Empty;

        if (memoryOwner is not null && memoryOwner.Memory.Length >= minimumLength)
        {
            return memoryOwner.Memory;
        }

        if (minimumLength > pool.MaxBufferSize)
        {
            Metrics.TooLargeRent(minimumLength, pool);
            return GC.AllocateUninitializedArray<T>(minimumLength);
        }

        if (memoryOwner is null)
        {
            return (memoryOwner = pool.Rent(minimumLength)).Memory;
        }

        IMemoryOwner<T> newMemory = pool.Rent(minimumLength);

        if (copyOnResize)
        {
            memoryOwner.Memory.CopyTo(newMemory.Memory);
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
        if (array.Length != 0) _disposed = true;
#endif
    }
}

internal class HeapMemoryPool<T> : MemoryPool<T>
{
    public static HeapMemoryPool<T> Instance { get; } = new();

    // ReSharper disable once UnusedMember.Global
    [Obsolete("Use Instance instead, this returns MemoryPool<T>.Shared", true)]
    public new static MemoryPool<T> Shared => throw new NotSupportedException();

    public override int MaxBufferSize => Array.MaxLength;

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize == 0)
            return HeapMemoryOwner<T>.Empty;

        if (minBufferSize == -1)
            minBufferSize = Environment.SystemPageSize;

        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);
        T[] array = GC.AllocateUninitializedArray<T>((int)BitOperations.RoundUpToPowerOf2((uint)minBufferSize));
        return new HeapMemoryOwner<T>(array);
    }

    protected override void Dispose(bool disposing)
    {
        // no-op
    }
}
