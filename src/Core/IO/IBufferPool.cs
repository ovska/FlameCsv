using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO;

/// <summary>
/// Pool for renting buffers for IO operations, unescaping values, and other temporary use.
/// </summary>
/// <remarks>
/// This type is intentionally not disposable; if a custom implementation needs disposal,
/// it should manage its own lifetime.
/// </remarks>
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
        IMemoryOwner<T> result;

        if (typeof(T) == typeof(byte))
        {
            result = Unsafe.As<IMemoryOwner<T>>(pool.GetBytes(length));
        }
        else if (typeof(T) == typeof(char))
        {
            result = Unsafe.As<IMemoryOwner<T>>(pool.GetChars(length));
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        Check.GreaterThanOrEqual(result.Memory.Length, length);
        return result;
    }

    /// <summary>
    /// Ensures that the memory owner has at least the specified minimum length, and returns the memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Memory<T> EnsureCapacity<T>(
        this IBufferPool pool,
        [AllowNull] ref IMemoryOwner<T> memoryOwner,
        int minimumLength,
        bool copyOnResize = false
    )
        where T : unmanaged
    {
        if (minimumLength <= 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
            return Memory<T>.Empty;
        }

        if (memoryOwner is null)
        {
            return (memoryOwner = pool.Rent<T>(minimumLength)).Memory;
        }

        Memory<T> currentMemory = memoryOwner.Memory;

        if (currentMemory.Length >= minimumLength)
        {
            return currentMemory;
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
