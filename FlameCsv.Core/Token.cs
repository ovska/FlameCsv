using System.Runtime.CompilerServices;

namespace FlameCsv;

internal static class Token<T> where T : unmanaged
{
    public static readonly string Name = typeof(T) == typeof(char)
        ? "char"
        : typeof(T) == typeof(byte)
            ? "byte"
            : typeof(T).Name;

    /// <summary>
    /// Returns true if <see langword="stackalloc"/> <typeparamref name="T"/>[<paramref name="length"/>] is reasonably safe.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length)
    {
        // JITed to a constant
        return (uint)length <= 512u / (uint)Unsafe.SizeOf<T>();
    }

    /// <summary>
    /// Conservative <see langword="stackalloc"/> size.
    /// </summary>
    public static int StackLength => 256 / Unsafe.SizeOf<T>();
}
