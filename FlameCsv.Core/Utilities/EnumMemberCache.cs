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
    private protected static TEnum?[]? _firstTenValues;

    /// <summary>
    /// Returns <c>true</c> if the specified enum type and format are supported.
    /// </summary>
    public static bool IsSupported(string? format)
    {
        // as of .NET9, Enum.TryFormat ignores to format provider parameter
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
                _firstTenValues = null;
                _canUseFastPath = null;
            });
    }

    private static ImmutableArray<EnumMember> _valuesAndNames;

    // ReSharper disable once StaticMemberInGenericType
    private static bool? _canUseFastPath;

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

            for (int i = 0; i < names.Length; i++)
            {
                if (EqualityComparer<TEnum>.Default.Equals(values[i], value))
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
                (enumMember.ExplicitName is not null && !uniqueNames.Add(enumMember.ExplicitName)))
            {
                duplicates.Add(enumMember);
            }

            if (enumMember.ExplicitName is not null &&
                (enumMember.ExplicitName.Length == 0 ||
                char.IsAsciiDigit(enumMember.ExplicitName[0]) ||
                enumMember.ExplicitName[0] == '-'))
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

    /// <summary>
    /// Fast path for single-digit values in the range of 0-9.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetFast(ReadOnlySpan<T> span, out TEnum value)
    {
        if (span.Length == 1 && CanUseFastPath)
        {
            var index = uint.CreateTruncating(span[0] - T.CreateTruncating('0'));

            if (index <= 9)
            {
                TEnum? result = FirstTenValues[index];

                if (result.HasValue)
                {
                    value = result.Value;
                    return true;
                }
            }
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    // protect against weirndess of using 0-9 numerics in enum member name
    private static bool CanUseFastPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _canUseFastPath ??= GetFastPath();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GetFastPath()
    {
        return ValuesAndNames.All(x => x.ExplicitName is not [>= '0' and <= '9']);
    }

    private static TEnum?[] FirstTenValues => _firstTenValues ??= GetFirstTenValues();

    private static TEnum?[] GetFirstTenValues()
    {
        TEnum?[] values = new TEnum?[10];
        values.AsSpan().Clear(); // in case we ever add SkipInit

        foreach ((TEnum value, _, _) in ValuesAndNames)
        {
            if (Convert.ToInt64(value) is var asInt64 and >= 0 and <= 9)
            {
                values[asInt64] = value;
            }
        }

        return values;
    }
}
