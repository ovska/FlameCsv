using System.Runtime.CompilerServices;

namespace FlameCsv;

internal static class Token<T> where T : unmanaged
{
    /// <summary>
    /// Safe upper limit for <see langword="stackalloc"/> for <typeparamref name="T"/>.
    /// </summary>
    public static readonly int StackallocThreshold = Unsafe.SizeOf<byte>() * 256 / Unsafe.SizeOf<T>();

    /// <summary>
    /// Returns true if <paramref name="length"/> is not greater than <see cref="StackallocThreshold"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length) => StackallocThreshold >= length;
}
