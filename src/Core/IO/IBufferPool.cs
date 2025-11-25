using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO;

/// <summary>
/// Pool for renting buffers for IO operations, unescaping values, and other temporary use.
/// </summary>
public interface IBufferPool
{
    /// <summary>
    /// Returns a byte buffer with at least the specified length.
    /// </summary>
    IMemoryOwner<byte> GetBytes(int length);

    /// <summary>
    /// Returns a char buffer with at least the specified length.
    /// </summary>
    IMemoryOwner<char> GetChars(int length);
}

internal sealed class DefaultBufferPool : IBufferPool
{
    public static DefaultBufferPool Instance { get; } = new DefaultBufferPool();

    public IMemoryOwner<byte> GetBytes(int length) => MemoryPool<byte>.Shared.Rent(length);

    public IMemoryOwner<char> GetChars(int length) => MemoryPool<char>.Shared.Rent(length);
}

internal static class BufferPoolExtensions
{
    public static IMemoryOwner<T> Rent<T>(this IBufferPool pool, int length)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<IMemoryOwner<T>>(pool.GetBytes(length));
        }
        if (typeof(T) == typeof(char))
        {
            return Unsafe.As<IMemoryOwner<T>>(pool.GetChars(length));
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Memory<T> EnsureCapacity<T>(
        this IBufferPool pool,
        [AllowNull] ref IMemoryOwner<T> memoryOwner,
        int minimumLength,
        bool copyOnResize = false
    )
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        if (minimumLength == 0)
            return Memory<T>.Empty;

        Memory<T> currentMemory = memoryOwner?.Memory ?? Memory<T>.Empty;

        if (currentMemory.Length >= minimumLength)
        {
            return currentMemory;
        }

        if (memoryOwner is null)
        {
            return (memoryOwner = pool.Rent<T>(minimumLength)).Memory;
        }

        IMemoryOwner<T> newMemory = pool.Rent<T>(minimumLength);

        if (copyOnResize)
        {
            currentMemory.CopyTo(newMemory.Memory);
        }

        memoryOwner.Dispose();
        memoryOwner = newMemory;
        return newMemory.Memory;
    }
}
