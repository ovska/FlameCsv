using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

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
