using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Extensions;

internal static class ArrayPoolExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsMemory<T>(in this ReadOnlySequence<T> sequence, ref T[]? array, ArrayPool<T> arrayPool)
    {
        if (sequence.IsSingleSegment)
            return sequence.First;

        int length = (int)sequence.Length;

        if (length == 0)
            return ReadOnlyMemory<T>.Empty;

        arrayPool.EnsureCapacity(ref array, length);
        sequence.CopyTo(array);
        return new ReadOnlyMemory<T>(array, 0, length);
    }

    /// <summary>
    /// If <paramref name="array"/> is not null, returns it to the pool and sets the reference to null.
    /// </summary>
    /// <param name="arrayPool">Pool the array was rented from</param>
    /// <param name="array">Array to return</param>
    /// <param name="clearArray">Whether the array should be cleared if returned</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureReturned<T>(this ArrayPool<T> arrayPool, ref T[]? array, bool clearArray = false)
    {
        if (array is not null)
        {
            arrayPool.Return(array, clearArray);
            array = null;
        }
    }

    public static MemoryPool<T> AsMemoryPool<T>(this ArrayPool<T> arrayPool)
    {
        return ReferenceEquals(arrayPool, ArrayPool<T>.Shared)
             ? MemoryPool<T>.Shared
             : new ArrayPoolMemoryPoolWrapper<T>(arrayPool);
    }

    private sealed class ArrayPoolMemoryPoolWrapper<T> : MemoryPool<T>
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
}
