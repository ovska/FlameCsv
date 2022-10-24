using System.Reflection;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static class ReflectionExtensions
{
    public static bool HasAttribute<TAttribute>(this MemberInfo memberInfo) where TAttribute : Attribute
    {
        return memberInfo.GetCustomAttributes().Any(static a => a is TAttribute);
    }

    public static MemberInfo GetPropertyOrField(
        this Type targetType,
        string MemberName,
        BindingFlags bindingFlags = CsvBindingConstants.MemberLookupFlags)
    {
        return targetType.GetProperty(MemberName, bindingFlags)
            ?? (MemberInfo?)targetType.GetField(MemberName, bindingFlags)
            ?? throw new InvalidOperationException($"Property/field {MemberName} not found on type {targetType}");
    }
}
