using System.Reflection;

namespace FlameCsv.Extensions;

internal static class ReflectionExtensions
{
    public static bool HasAttribute<TAttribute>(this MemberInfo memberInfo) where TAttribute : Attribute
    {
        return memberInfo.GetCustomAttributes().Any(static a => a is TAttribute);
    }
}
