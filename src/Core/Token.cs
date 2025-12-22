using System.Diagnostics;
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
        uint threshold = 512u / (uint)Unsafe.SizeOf<T>();
        return (uint)length <= threshold;
    }

    public static NotSupportedException NotSupported
    {
        get
        {
            Check.True(
                typeof(T) != typeof(char) && typeof(T) != typeof(byte),
                "Token<T>.NotSupported should not be called for char or byte tokens."
            );

            return new($"Token type {typeof(T).FullName} is not supported.");
        }
    }
}
