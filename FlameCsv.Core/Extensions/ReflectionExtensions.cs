using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

internal static class ReflectionExtensions
{
    /// <summary>
    /// Binding flags used by the library to look for binding attributes in properties and fields.
    /// </summary>
    public const BindingFlags MemberLookupFlags = BindingFlags.Instance | BindingFlags.Public;

    private static readonly ConditionalWeakTable<MemberInfo, object[]> _attributesCache = new();
    private static readonly ConditionalWeakTable<Type, MemberInfo[]> _membersCache = new();

    internal static MemberInfo[] GetCachedPropertiesAndFields(this Type type)
    {
        if (!_membersCache.TryGetValue(type, out var members))
        {
            members = type
                .GetMembers(MemberLookupFlags)
                .Where(m => m is PropertyInfo or FieldInfo)
                .ToArray();
            _membersCache.AddOrUpdate(type, members);
        }

        return members;
    }

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
        string memberName)
    {
        foreach (var member in targetType.GetCachedPropertiesAndFields())
        {
            if (member.Name.Equals(memberName))
                return member;
        }

        return ThrowHelper.ThrowInvalidOperationException<MemberInfo>(
            $"Property/field {memberName} not found on type {targetType}");
    }
}
