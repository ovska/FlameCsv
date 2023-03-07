using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Extensions;

internal static class ReflectionExtensions
{
    /// <summary>
    /// Binding flags used by the library to look for binding attributes in properties and fields.
    /// </summary>
    public const BindingFlags MemberLookupFlags = BindingFlags.Instance | BindingFlags.Public;

    private static readonly ConditionalWeakTable<MemberInfo, object[]> _memberAttrCache = new();
    private static readonly ConditionalWeakTable<ParameterInfo, object[]> _paramAttrCache = new();
    private static readonly ConditionalWeakTable<Type, MemberInfo[]> _membersCache = new();
    private static readonly ConditionalWeakTable<ConstructorInfo, ParameterInfo[]> _ctorParamCache = new();

    internal static ParameterInfo[] GetCachedParameters(this ConstructorInfo constructor)
    {
        if (!_ctorParamCache.TryGetValue(constructor, out var parameters))
        {
            parameters = constructor.GetParameters();

            if (parameters.Length == 0)
            {
                parameters = Array.Empty<ParameterInfo>();
            }
            else
            {
                parameters.AsSpan().Sort((a, b) => a.Position.CompareTo(b.Position));
            }

            _ctorParamCache.AddOrUpdate(constructor, parameters);
        }

        return parameters;
    }

    internal static MemberInfo[] GetCachedPropertiesAndFields(this Type type)
    {
        if (!_membersCache.TryGetValue(type, out var members))
        {
            members = type
                .GetMembers(MemberLookupFlags)
                .Where(static m => m is PropertyInfo or FieldInfo)
                .ToArray();
            _membersCache.AddOrUpdate(type, members);
        }

        return members;
    }

    internal static object[] GetCachedCustomAttributes(this MemberInfo obj)
    {
        if (!_memberAttrCache.TryGetValue(obj, out var attributes))
        {
            _memberAttrCache.AddOrUpdate(obj, attributes = obj.GetCustomAttributes(inherit: true));
        }

        return attributes;
    }

    internal static object[] GetCachedParameterAttributes(this ParameterInfo obj)
    {
        if (!_paramAttrCache.TryGetValue(obj, out var attributes))
        {
            _paramAttrCache.AddOrUpdate(obj, attributes = obj.GetCustomAttributes(inherit: false));
        }

        return attributes;
    }

    public static bool HasAttribute<TAttribute>(this MemberInfo obj) where TAttribute : Attribute
    {
        return obj.HasAttribute<TAttribute>(out _);
    }

    public static bool HasAttribute<TAttribute>(this MemberInfo obj, [NotNullWhen(true)] out TAttribute? attribute)
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

        return ThrowMemberNotFound(targetType, memberName);
    }

    private static MemberInfo ThrowMemberNotFound(Type targetType, string memberName)
    {
        throw new CsvConfigurationException($"Property/field {memberName} not found on type {targetType}");
    }
}
