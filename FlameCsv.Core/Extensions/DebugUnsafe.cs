#if DEBUG
namespace FlameCsv.Extensions;

/// <summary>
/// Drop-in for <see cref="System.Runtime.CompilerServices.Unsafe"/> for validation in debug builds.
/// </summary>
internal static class DebugUnsafe
{
    public static int SizeOf<T>() where T : allows ref struct
    {
        return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
    }

    public static T As<T>(object value)
    {
        return (T)value;
    }

    public static TTo As<TFrom, TTo>(
        ref TFrom value)
        where TFrom : struct
        where TTo : struct
    {
        return (TTo)(object)value;
    }
}
#endif
