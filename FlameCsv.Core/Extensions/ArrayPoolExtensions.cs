using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.Extensions;

internal static class ArrayPoolExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TryReturn<T>(this ArrayPool<T> arrayPool, ref T[]? array, bool clearArray = false)
    {
        if (array is not null)
        {
            arrayPool.Return(array, clearArray);
            array = null;
        }
    }
}
