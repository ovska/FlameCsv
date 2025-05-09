using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv;

[ExcludeFromCodeCoverage]
internal static class Token<T>
    where T : unmanaged
{
    public static string Name =>
        typeof(T) == typeof(char) ? "char"
        : typeof(T) == typeof(byte) ? "byte"
        : typeof(T).Name;

    /// <summary>
    /// Returns true if <see langword="stackalloc"/> <typeparamref name="T"/>[<paramref name="length"/>] is reasonably safe.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanStackalloc(int length)
    {
        return (uint)length <= 512u / (uint)Unsafe.SizeOf<T>();
    }

    public static NotSupportedException NotSupported => new($"Token type {typeof(T).Name} is not supported.");
}
