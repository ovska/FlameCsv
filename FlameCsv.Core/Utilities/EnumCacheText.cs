using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Utilities;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class EnumCacheText<TEnum> : EnumMemberCache<char, TEnum> where TEnum : struct, Enum
{
    [ExcludeFromCodeCoverage]
    static EnumCacheText()
    {
        HotReloadService.RegisterForHotReload(
            typeof(TEnum),
            static _ =>
            {
                _valuesNormalOrdinal = null;
                _valuesExplicitOrdinal = null;
                _valuesNormalIgnoreCase = null;
                _valuesExplicitIgnoreCase = null;
                _namesNumeric = null;
                _namesString = null;
                _namesExplicit = null;
            });
    }

    private static FrozenDictionary<string, TEnum>? _valuesNormalOrdinal;
    private static FrozenDictionary<string, TEnum>? _valuesExplicitOrdinal;
    private static FrozenDictionary<string, TEnum>? _valuesNormalIgnoreCase;
    private static FrozenDictionary<string, TEnum>? _valuesExplicitIgnoreCase;

    private static FrozenDictionary<TEnum, string>? _namesNumeric;
    private static FrozenDictionary<TEnum, string>? _namesString;
    private static FrozenDictionary<TEnum, string>? _namesExplicit;

    public static FrozenDictionary<TEnum, string>? GetWriteValues(string? format, bool useEnumMember)
    {
        return (GetFormatChar(format), useEnumMember) switch
        {
            ('g', false) => _namesString ??= InitNames(ToString),
            ('g', true) => _namesExplicit ??= InitNames(ToExplicit),
            ('d', _) => _namesNumeric ??= InitNames(ToNumber),
            _ => null,
        };
    }

    public static FrozenDictionary<string, TEnum>.AlternateLookup<ReadOnlySpan<char>> GetReadValues(
        bool ignoreCase,
        bool useEnumMember)
    {
        return ((ignoreCase, useEnumMember) switch
        {
            (false, false) => _valuesNormalOrdinal ??= InitValues(false, false),
            (false, true) => _valuesExplicitOrdinal ??= InitValues(false, true),
            (true, false) => _valuesNormalIgnoreCase ??= InitValues(true, false),
            (true, true) => _valuesExplicitIgnoreCase ??= InitValues(true, true),
        }).GetAlternateLookup<ReadOnlySpan<char>>();
    }

    private static FrozenDictionary<TEnum, string> InitNames(Func<EnumMember, string> selector)
    {
        Dictionary<TEnum, string> dict = [];

        foreach (var entry in ValuesAndNames)
        {
            dict[entry.Value] = selector(entry);
        }

        return dict.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, TEnum> InitValues(bool ignoreCase, bool enumMember)
    {
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, TEnum> valuesByName = new(comparer);

        foreach ((TEnum value, string name, string? _) in ValuesAndNames)
        {
            valuesByName.TryAdd(value.ToString("D"), value); // numbers are always ASCII
            valuesByName[name] = value;
        }

        // add overrides last, just in case someone does something bizarre like [EnumMember(Value = "1")]
        if (enumMember)
        {
            foreach ((TEnum value, _, string? explicitName) in ValuesAndNames)
            {
                if (explicitName is not null)
                {
                    valuesByName[explicitName] = value;
                }
            }
        }

        return valuesByName.ToFrozenDictionary(comparer);
    }

    private static string ToNumber(EnumMember member) => member.Value.ToString("D");
    private static string ToString(EnumMember member) => member.Name;
    private static string ToExplicit(EnumMember member) => member.ExplicitName ?? member.Name;
}
