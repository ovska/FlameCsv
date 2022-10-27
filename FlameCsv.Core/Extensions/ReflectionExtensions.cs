using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static class ReflectionExtensions
{
    private static readonly ConditionalWeakTable<object, object[]> _attributesCache = new();

    internal static object[] GetCachedCustomAttributes(this MemberInfo obj)
    {
        if (!_attributesCache.TryGetValue(obj, out var attributes))
        {
            // TODO: revisit whether inherit should be true
            _attributesCache.AddOrUpdate(obj, attributes = obj.GetCustomAttributes(inherit: false));
        }

        return attributes;
    }

    public static bool HasAttribute<TAttribute>(this MemberInfo obj) where TAttribute : Attribute
    {
        return obj.HasAttribute<TAttribute>(out _);
    }

    public static bool HasAttribute<TAttribute>(
        this MemberInfo obj,
        [NotNullWhen(true)] out TAttribute? attribute)
        where TAttribute : Attribute
    {
        foreach (var a in obj.GetCachedCustomAttributes())
        {
            if (a is TAttribute _a)
            {
                attribute = _a;
                return true;
            }
        }

        attribute = null;
        return false;
    }

    public static MemberInfo GetPropertyOrField(
        this Type targetType,
        string memberName,
        BindingFlags bindingFlags = CsvBindingConstants.MemberLookupFlags)
    {
        return targetType.GetProperty(memberName, bindingFlags)
            ?? (MemberInfo?)targetType.GetField(memberName, bindingFlags)
            ?? throw new InvalidOperationException($"Property/field {memberName} not found on type {targetType}");
    }
}
