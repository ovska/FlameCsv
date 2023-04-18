using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv;

internal static class Token<T> where T : unmanaged
{
    /// <summary>
    /// Safe upper limit for <see langword="stackalloc"/> for <typeparamref name="T"/>.
    /// </summary>
    // JITed to a constant
    public static int StackallocThreshold
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.SizeOf<byte>() * 512 / Unsafe.SizeOf<T>();
    }

    /// <summary>
    /// Returns true if <paramref name="length"/> is not greater than <see cref="StackallocThreshold"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length) => StackallocThreshold >= length;

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn]
    public static void ThrowNotSupportedException()
    {
        throw new NotSupportedException($"The current operation for {typeof(T).FullName} is not supported by default.");
    }
}
