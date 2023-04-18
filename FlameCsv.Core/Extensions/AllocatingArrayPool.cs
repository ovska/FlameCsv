using System.Buffers;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Extensions;

/// <summary>
/// Array "pool" that always allocates a new array.
/// </summary>
internal sealed class AllocatingArrayPool<T> : ArrayPool<T> where T : unmanaged
{
    /// <inheritdoc cref="AllocatingArrayPool{T}"/>
    public static AllocatingArrayPool<T> Instance { get; } = new();

    private AllocatingArrayPool() { }

    public override T[] Rent(int minimumLength)
    {
        Guard.IsGreaterThanOrEqualTo(minimumLength, 0);
        return minimumLength > 0
            ? new T[BitOperations.RoundUpToPowerOf2((uint)minimumLength)]
            : Array.Empty<T>();
    }

    public override void Return(T[] array, bool clearArray = false)
    {
    }
}

internal sealed class ArrayPoolMemoryPoolWrapper<T> : MemoryPool<T>
{
    private readonly ArrayPool<T> _arrayPool;

    public override int MaxBufferSize => Array.MaxLength;

    public ArrayPoolMemoryPoolWrapper(ArrayPool<T> arrayPool)
    {
        ArgumentNullException.ThrowIfNull(arrayPool);
        _arrayPool = arrayPool;
    }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        return minBufferSize switch
        {
            -1 => MemoryOwner<T>.Allocate(4096, _arrayPool),
            _ => MemoryOwner<T>.Allocate(minBufferSize, _arrayPool),
        };
    }

    protected override void Dispose(bool disposing)
    {
    }
}

