using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

internal static class GuardEx
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsDefined<TEnum>(TEnum value, [CallerArgumentExpression("value")] string name = "")
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            ThrowHelper.ThrowArgumentOutOfRangeException(name, value, null);
    }
}
