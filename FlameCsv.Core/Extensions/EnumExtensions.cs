using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace FlameCsv.Extensions;

internal readonly record struct EnumValue<TEnum>(TEnum Value, string Name, string? Member) where TEnum : struct, Enum;

internal static class EnumExtensions
{
    private static readonly ConditionalWeakTable<Type, object> _enumCache = new();

    public static EnumValue<TEnum>[] GetEnumMembers<TEnum>()
        where TEnum : struct, Enum
    {
        if (!_enumCache.TryGetValue(typeof(TEnum), out var members))
        {
            _enumCache.AddOrUpdate(typeof(TEnum), members = GetMembersInternal<TEnum>());
        }

        return (EnumValue<TEnum>[])members;
    }

    private static EnumValue<TEnum>[] GetMembersInternal<TEnum>()
        where TEnum : struct, Enum
    {
        var fields = typeof(TEnum).GetFields();
        return Enum.GetNames<TEnum>()
            .Select(name => new EnumValue<TEnum>(
                Enum.Parse<TEnum>(name),
                name,
                fields.Single(f => f.Name.Equals(name)).GetCustomAttribute<EnumMemberAttribute>()?.Value
            ))
            .ToArray()
            .ForCache();
    }
}
