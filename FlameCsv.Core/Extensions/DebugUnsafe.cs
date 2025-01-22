#if DEBUG
namespace FlameCsv.Extensions;

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

    public static void SkipInit<T>(out T value) => value = default!;
}
#endif
