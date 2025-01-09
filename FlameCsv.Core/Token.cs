using System.Runtime.CompilerServices;

namespace FlameCsv;

internal static class Token<T> where T : unmanaged
{
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
