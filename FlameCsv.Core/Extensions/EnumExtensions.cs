using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv.Extensions;

internal static class EnumExtensions
{
    private static readonly ConditionalWeakTable<Type, object> _enumCache = new();

    /// <summary>
    /// Returns true if <see cref="SecurityLevel.NoBufferClearing"/> is not set.
    /// </summary>
    public static bool ClearBuffers(this SecurityLevel security)
    {
        return (security & SecurityLevel.NoBufferClearing) != SecurityLevel.NoBufferClearing;
    }

    public static (TEnum value, string name, string? enumMember)[] GetEnumMembers<TEnum>()
        where TEnum : struct, Enum
    {
        if (!_enumCache.TryGetValue(typeof(TEnum), out var members))
        {
            members = GetMembersInternal<TEnum>().ToArray();
            _enumCache.AddOrUpdate(typeof(TEnum), members);
        }

        return ((TEnum value, string name, string? enumMember)[])members;
    }

    private static IEnumerable<(TEnum value, string name, string? enumMember)> GetMembersInternal<TEnum>()
        where TEnum : struct, Enum
    {
        var fields = typeof(TEnum).GetFields();

        foreach (var name in Enum.GetNames<TEnum>())
        {
            yield return (
                Enum.Parse<TEnum>(name),
                name,
                fields.Single(f => f.Name.Equals(name)).GetCustomAttribute<EnumMemberAttribute>()?.Value
            );
        }
    }
}
