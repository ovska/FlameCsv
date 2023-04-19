using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Extensions;

internal static class ArrayPoolExtensions
{
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

    public static Memory<T> GetUnescapeBuffer<T>(
        this ArrayPool<T> arrayPool,
        ref T[]? buffer,
        int valueLength,
        int quoteCount)
    {
        if (quoteCount != 0)
        {
            arrayPool.EnsureCapacity(ref buffer, valueLength - quoteCount / 2);
            return buffer;
        }

        return default;
    }
}
