using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using FlameCsv.Exceptions;

namespace FlameCsv.Utilities;

internal abstract class EnumMemberCache<[DAM(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    where TEnum : struct, Enum
{
    public static bool HasFlagsAttribute
        => _hasFlagsAttribute ??= typeof(TEnum).GetCustomAttribute<FlagsAttribute>() is not null;

    // ReSharper disable once StaticMemberInGenericType
    private protected static bool? _hasFlagsAttribute;

    /// <summary>
    /// Returns <c>true</c> if the specified enum type and format are supported.
    /// </summary>
    public static bool IsSupported(string? format)
    {
        return !HasFlagsAttribute && !"F".Equals(format, StringComparison.OrdinalIgnoreCase);
    }

    internal readonly record struct EnumMember(TEnum Value, string Name, string? ExplicitName)
    {
        public static implicit operator TEnum(EnumMember member) => member.Value;

        public override string ToString()
        {
            using var vsb = new ValueStringBuilder(stackalloc char[64]);

            vsb.Append(typeof(TEnum).Name);
            vsb.Append('.');
            vsb.Append(Name);
            vsb.Append(" (");
            vsb.AppendFormatted(Value, "D");
            vsb.Append(')');

            if (!string.IsNullOrEmpty(ExplicitName))
            {
                vsb.Append(" = \"");
                vsb.Append(ExplicitName);
                vsb.Append('"');
            }

            return vsb.ToString();
        }
    }
}

internal abstract class EnumMemberCache<T, [DAM(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    : EnumMemberCache<TEnum>
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum
{
    [ExcludeFromCodeCoverage]
    static EnumMemberCache()
    {
        HotReloadService.RegisterForHotReload(
            typeof(TEnum),
            static _ =>
            {
                _hasFlagsAttribute = null;
                _valuesAndNames = default;
            });
    }

    private static ImmutableArray<EnumMember> _valuesAndNames;

    protected static char GetFormatChar(string? format)
    {
        if (HasFlagsAttribute)
        {
            throw new UnreachableException($"{nameof(GetFormatChar)} called on flags-enum {typeof(TEnum)}.");
        }

        if (string.IsNullOrEmpty(format))
        {
            return 'g';
        }

        ReadOnlySpan<char> fmt = format.AsSpan();

        if (fmt.Length == 1)
        {
            char c = (char)(fmt[0] | 0x20);

            if (c is 'g' or 'd' or 'x')
            {
                return c;
            }

            if (c is 'f')
            {
                throw new NotSupportedException("Flags-format is not supported for non-flags enums.");
            }
        }

        throw new FormatException($"Invalid enum format string: {format}");
    }

    internal static ImmutableArray<EnumMember> ValuesAndNames
    {
        get => _valuesAndNames.IsDefault ? (_valuesAndNames = GetValuesAndNames()) : _valuesAndNames;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ImmutableArray<EnumMember> GetValuesAndNames()
    {
        string[] names = Enum.GetNames<TEnum>();
        TEnum[] values = Enum.GetValues<TEnum>();

        var builder = ImmutableArray.CreateBuilder<EnumMember>(values.Length);

        HashSet<string> uniqueNames = [];
        List<EnumMember> duplicates = [];

        foreach (var f in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not TEnum value) continue;

            int index = -1;

            for (int i = 0; i < values.Length; i++)
            {
                if (names[i] == f.Name && EqualityComparer<TEnum>.Default.Equals(values[i], value))
                {
                    index = i;
                    break;
                }
            }

            if (index == -1) continue;

            var enumMember = new EnumMember(
                value,
                names[index],
                f.GetCustomAttribute<EnumMemberAttribute>()?.Value);

            if (!uniqueNames.Add(enumMember.Name) ||
                (
                    enumMember.ExplicitName is not null &&
                    enumMember.ExplicitName != enumMember.Name &&
                    !uniqueNames.Add(enumMember.ExplicitName)
                ))
            {
                duplicates.Add(enumMember);
            }

            if (enumMember.ExplicitName is not null &&
                (
                    enumMember.ExplicitName.Length == 0 ||
                    char.IsAsciiDigit(enumMember.ExplicitName[0]) ||
                    enumMember.ExplicitName[0] == '-'
                ))
            {
                throw new CsvConfigurationException(
                    $"Enum member name '{enumMember.ExplicitName}' for {typeof(TEnum).FullName} cannot be empty or start with a digit or '-' character.");
            }

            builder.Add(enumMember);
        }

        if (duplicates.Count > 0)
        {
            duplicates.Sort((x, y) => Comparer<TEnum>.Default.Compare(x.Value, y.Value));
            throw new CsvConfigurationException(
                $"Duplicate enum names configured for {typeof(TEnum).FullName}: {string.Join(", ", duplicates)}");
        }

        return builder.ToImmutable();
    }
}
