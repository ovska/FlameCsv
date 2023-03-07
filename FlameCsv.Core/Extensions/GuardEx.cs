using System.Reflection;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsNotInterface(Type type)
    {
        if (type.IsInterface)
            ThrowHelper.ThrowNotSupportedException("Interface binding is not yet supported.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsNotInterfaceDefined(MemberInfo member)
    {
        if (member.DeclaringType is { IsInterface: true })
            ThrowHelper.ThrowNotSupportedException("Interface binding is not yet supported.");
    }
}
