using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters.Enums;
using FlameCsv.Utilities;

namespace FlameCsv.Reflection;

internal sealed class EnumCacheText<TEnum> : EnumMemberCache<TEnum>
    where TEnum : struct, Enum
{
    [ExcludeFromCodeCoverage]
    static EnumCacheText()
    {
        HotReloadService.RegisterForHotReload(
            typeof(TEnum),
            static _ =>
            {
                _valuesOrdinal = null;
                _valuesIgnoreCase = null;
                _namesNumeric = null;
                _namesString = null;
            }
        );
    }

    private static FrozenDictionary<string, TEnum>? _valuesOrdinal;
    private static FrozenDictionary<string, TEnum>? _valuesIgnoreCase;

    private static FrozenDictionary<TEnum, string>? _namesNumeric;
    private static FrozenDictionary<TEnum, string>? _namesString;

    public static FrozenDictionary<TEnum, string>? GetWriteValues(string? format)
    {
        return (GetFormatChar(format)) switch
        {
            'g' or 'f' => _namesString ??= InitNames(ToString),
            'd' => _namesNumeric ??= InitNames(ToNumber),
            _ => null,
        };
    }

    public static FrozenDictionary<string, TEnum>.AlternateLookup<ReadOnlySpan<char>> GetReadValues(bool ignoreCase)
    {
        return (
            ignoreCase switch
            {
                false => _valuesOrdinal ??= InitValues(false),
                true => _valuesIgnoreCase ??= InitValues(true),
            }
        ).GetAlternateLookup<ReadOnlySpan<char>>();
    }

    private static FrozenDictionary<TEnum, string> InitNames(Func<EnumMember, string> selector)
    {
        Dictionary<TEnum, string> dict = [];

        foreach (var entry in ValuesAndNames)
        {
            // if there are aliased names, use the first one, e.g., enum Animal { Dog = 1, Canine = Dog }
            dict.TryAdd(entry.Value, selector(entry));
        }

        return dict.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, TEnum> InitValues(bool ignoreCase)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, TEnum> valuesByName = new(comparer);

        foreach ((TEnum value, string name, string? explicitName) in ValuesAndNames)
        {
            valuesByName[name] = value;

            if (explicitName is not null)
            {
                valuesByName[explicitName] = value;
            }
        }

        return valuesByName.ToFrozenDictionary(comparer);
    }

    private static string ToNumber(EnumMember member) => member.Value.ToString("D");

    private static string ToString(EnumMember member) => member.ExplicitName ?? member.Name;

    public static bool IsDefinedCore(TEnum value)
    {
        if (HasFlagsAttribute)
        {
            return (value.ToBitmask() & ~AllFlags.ToBitmask()) == 0;
        }

        return GetWriteValues("D")?.ContainsKey(value) ?? Enum.IsDefined(value);
    }
}
