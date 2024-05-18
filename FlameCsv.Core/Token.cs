using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv;

internal static class Token<T> where T : unmanaged
{
    /// <summary>
    /// Returns true if an array of <paramref name="length"/> would be likely to be allocated on the Large Object Heap.
    /// </summary>
    public static bool LargeObjectHeapAllocates(int length)
    {
        // JITed to a constant
        uint threshold = sizeof(byte) * 84_000u / (uint)Unsafe.SizeOf<T>();
        return (uint)length > threshold;
    }

    /// <summary>
    /// Returns true if <see langword="stackalloc"/> <typeparamref name="T"/>[<paramref name="length"/>] is reasonably safe.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length)
    {
        // JITed to a constant
        uint threshold = sizeof(byte) * 512u / (uint)Unsafe.SizeOf<T>();
        return (uint)length <= threshold;
    }

    /// <summary>
    /// Conservative <see langword="stackalloc"/> size.
    /// </summary>
    public static int StackLength => sizeof(byte) * 256 / Unsafe.SizeOf<T>();
}
