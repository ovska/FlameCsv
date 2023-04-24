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
        int threshold = Unsafe.SizeOf<byte>() * 84_000 / Unsafe.SizeOf<T>();
        return length > threshold;
    }

    /// <summary>
    /// Returns true if a stack allocation with <paramref name="length"/> is reasonably safe.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length)
    {
        // JITed to a constant
        int threshold = Unsafe.SizeOf<byte>() * 512 / Unsafe.SizeOf<T>();
        return length <= threshold;
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn]
    public static void ThrowNotSupportedException()
    {
        throw new NotSupportedException($"The current operation for {typeof(T).ToTypeString()} is not supported by default.");
    }
}
