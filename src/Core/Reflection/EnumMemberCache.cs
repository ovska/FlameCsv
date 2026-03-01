using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using FlameCsv.Converters.Enums;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;

namespace FlameCsv.Reflection;

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter

internal abstract class EnumMemberCache<[DAM(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    where TEnum : struct, Enum
{
    public static void EnsureFlagsAttribute()
    {
        if (!HasFlagsAttribute)
        {
            throw new NotSupportedException($"Flags-format 'f' is not supported on non-flags enum {typeof(TEnum)}");
        }
    }

    public static bool HasFlagsAttribute { get; } = typeof(TEnum).GetCustomAttribute<FlagsAttribute>() is not null;

    public static TEnum AllFlags => _allFlags ??= InitFlags();

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
            vsb.Append(" = ");
            vsb.AppendFormatted(Value, "D");

            if (!string.IsNullOrEmpty(ExplicitName))
            {
                vsb.Append(" (");
                vsb.Append(ExplicitName);
                vsb.Append(')');
            }

            return vsb.ToString();
        }
    }

    private protected static TEnum? _allFlags;
    private protected static ImmutableArray<EnumMember> _valuesAndNames;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TEnum InitFlags()
    {
        if (!HasFlagsAttribute)
        {
            throw new NotSupportedException($"{typeof(TEnum).FullName} is not a flags enum.");
        }

        TEnum allFlags = default;

        foreach (var value in Enum.GetValues<TEnum>())
        {
            allFlags.AddFlag(value);
        }

        return allFlags;
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
        List<EnumMember>? duplicates = [];
        List<EnumMember>? invalidNames = [];

        foreach (var f in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not TEnum value)
                continue;

            int index = -1;

            for (int i = 0; i < values.Length; i++)
            {
                if (names[i] == f.Name && EqualityComparer<TEnum>.Default.Equals(values[i], value))
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                continue;

            EnumMember enumMember = new(value, names[index], f.GetCustomAttribute<EnumMemberAttribute>()?.Value);

            if (
                !uniqueNames.Add(enumMember.Name)
                || (
                    enumMember.ExplicitName is not null
                    && enumMember.ExplicitName != enumMember.Name
                    && !uniqueNames.Add(enumMember.ExplicitName)
                )
            )
            {
                (duplicates ??= []).Add(enumMember);
            }

            if (
                enumMember.ExplicitName is not null
                && (
                    enumMember.ExplicitName.Length == 0
                    || char.IsAsciiDigit(enumMember.ExplicitName[0])
                    || enumMember.ExplicitName[0] == '-'
                )
            )
            {
                (invalidNames ??= []).Add(enumMember);
                throw new CsvConfigurationException(
                    $"Enum member name '{enumMember.ExplicitName}' for {typeof(TEnum).FullName} cannot be empty or start with a digit or '-' character."
                );
            }

            builder.Add(enumMember);
        }

        if (duplicates.Count > 0 || invalidNames.Count > 0)
        {
            StringBuilder sb = new($"Invalid enum configuration for {typeof(TEnum).FullName}: ");
            duplicates.Sort((x, y) => Comparer<TEnum>.Default.Compare(x.Value, y.Value));

            if (duplicates.Count > 0)
            {
                sb.Append("Duplicate enum names: ");
                sb.AppendJoin(", ", duplicates);
                sb.Append(". ");
            }

            if (invalidNames.Count > 0)
            {
                sb.Append("Invalid enum names (cannot be empty, or start with a digit or '-' character): ");
                sb.AppendJoin(", ", invalidNames);
                sb.Append('.');
            }

            throw new CsvConfigurationException(sb.ToString());
        }

        return builder.ToImmutable();
    }

    protected static char GetFormatChar(string? format)
    {
        return EnumFormat.Get(format) ?? throw new FormatException($"Invalid enum format string: {format}");
    }

    public static void EnsureValidFlagsSeparator(char flagsSeparator)
    {
        if (!HasFlagsAttribute)
            throw new UnreachableException();

        Check.False(char.IsAsciiDigit(flagsSeparator), $"Flags-separator cannot be a digit: '{flagsSeparator}'");
        Check.False(char.IsAsciiLetter(flagsSeparator), $"Flags-separator cannot be a letter: '{flagsSeparator}'");
        Check.NotEqual(flagsSeparator, '-');

        foreach (var member in ValuesAndNames)
        {
            if (member.Name.Contains(flagsSeparator) || (member.ExplicitName?.Contains(flagsSeparator) is true))
            {
                throw new CsvConfigurationException(
                    $"Enum {typeof(TEnum).FullName}.{member.Name} name contains the flags-separator character '{flagsSeparator}'."
                );
            }
        }
    }
}

internal static class EnumFormat
{
    public static char? Get(string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return 'g';
        }

        if (format.Length == 1 && char.ToLowerInvariant(format[0]) is char c and ('g' or 'd' or 'x' or 'f'))
        {
            return c;
        }

        return null;
    }
}
