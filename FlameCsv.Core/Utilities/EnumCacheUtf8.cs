using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FlameCsv.Utilities.Comparers;

// ReSharper disable StaticMemberInGenericType

namespace FlameCsv.Utilities;

// strings cannot be directly used with other alternate comparers than ROS<char>
internal readonly struct StringLike
{
    public required string Value { get; init; }
    public static implicit operator StringLike(string value) => new() { Value = value };
    public static implicit operator string(StringLike value) => value.Value;
    public static implicit operator ReadOnlySpan<char>(StringLike value) => value.Value;
}

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class EnumCacheUtf8<TEnum> : EnumMemberCache<byte, TEnum>
    where TEnum : struct, Enum
{
    [ExcludeFromCodeCoverage]
    static EnumCacheUtf8()
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

    private static FrozenDictionary<StringLike, TEnum>? _valuesNormalOrdinal;
    private static FrozenDictionary<StringLike, TEnum>? _valuesExplicitOrdinal;
    private static FrozenDictionary<StringLike, TEnum>? _valuesNormalIgnoreCase;
    private static FrozenDictionary<StringLike, TEnum>? _valuesExplicitIgnoreCase;

    private static FrozenDictionary<TEnum, byte[]>? _namesNumeric;
    private static FrozenDictionary<TEnum, byte[]>? _namesString;
    private static FrozenDictionary<TEnum, byte[]>? _namesExplicit;

    public static FrozenDictionary<TEnum, byte[]>? GetWriteValues(string? format, bool useEnumMember)
    {
        return (GetFormatChar(format), useEnumMember) switch
        {
            ('g', false) => _namesString ??= InitNames(ToString),
            ('g', true) => _namesExplicit ??= InitNames(ToExplicit),
            ('d', _) => _namesNumeric ??= InitNames(ToNumber),
            _ => null,
        };
    }

    public static FrozenDictionary<StringLike, TEnum>.AlternateLookup<ReadOnlySpan<byte>> GetReadValues(
        bool ignoreCase,
        bool useEnumMember)
    {
        return ((ignoreCase, useEnumMember) switch
        {
            (false, false) => _valuesNormalOrdinal ??= InitValues(false, false),
            (false, true) => _valuesExplicitOrdinal ??= InitValues(false, true),
            (true, false) => _valuesNormalIgnoreCase ??= InitValues(true, false),
            (true, true) => _valuesExplicitIgnoreCase ??= InitValues(true, true),
        }).GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    private static FrozenDictionary<TEnum, byte[]> InitNames(Func<EnumMember, byte[]> selector)
    {
        Dictionary<TEnum, byte[]> dict = [];

        foreach (var entry in ValuesAndNames)
        {
            // if there are aliased names, use the first one, e.g., enum Animal { Dog = 1, Canine = Dog }
            dict.TryAdd(entry.Value, selector(entry));
        }

        return dict.ToFrozenDictionary();
    }

    private static FrozenDictionary<StringLike, TEnum> InitValues(bool ignoreCase, bool enumMember)
    {
        bool allAscii = true;
        var comparer = ignoreCase ? Utf8Comparer.OrdinalIgnoreCase : Utf8Comparer.Ordinal;
        Dictionary<StringLike, TEnum> valuesByName = new(comparer);

        foreach ((TEnum value, string name, string? explicitName) in ValuesAndNames)
        {
            valuesByName[name] = value;
            allAscii = allAscii && Ascii.IsValid(name);

            if (enumMember && explicitName is not null)
            {
                valuesByName[explicitName] = value;
                allAscii = allAscii && Ascii.IsValid(explicitName);
            }
        }

        IEqualityComparer<StringLike> nameComparer = (ignoreCase, allAscii) switch
        {
            (ignoreCase: true, allAscii: true) => IgnoreCaseAsciiComparer.Instance,
            (ignoreCase: true, allAscii: false) => Utf8Comparer.OrdinalIgnoreCase,
            (ignoreCase: false, allAscii: true) => OrdinalAsciiComparer.Instance,
            (ignoreCase: false, allAscii: false) => Utf8Comparer.Ordinal,
        };

        return valuesByName.ToFrozenDictionary(nameComparer);
    }

    private static byte[] ToNumber(EnumMember member) => Encoding.UTF8.GetBytes(member.Value.ToString("D"));
    private static byte[] ToString(EnumMember member) => Encoding.UTF8.GetBytes(member.Name);
    private static byte[] ToExplicit(EnumMember member) => Encoding.UTF8.GetBytes(member.ExplicitName ?? member.Name);
}
