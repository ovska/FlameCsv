using System.Runtime.CompilerServices;
using FlameCsv.Reflection;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Internal implementation detail, used by the source generator.
/// </summary>
public sealed class CsvEnumFlagsTextFormatStrategy<TEnum> : CsvEnumFlagsFormatStrategy<char, TEnum>
    where TEnum : struct, Enum
{
    // hot reload safe; enum members or attributes can't be changed at runtime

    // ReSharper disable once StaticMemberInGenericType
    private static string? _zero;

    /// <inheritdoc />
    public CsvEnumFlagsTextFormatStrategy(CsvOptions<char> options, EnumFormatStrategy<char, TEnum> inner)
        : base(options, inner) { }

    /// <inheritdoc />
    protected override ReadOnlySpan<char> Zero => _zero ?? InitSharedZero();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string InitSharedZero()
    {
        foreach (var member in EnumMemberCache<TEnum>.ValuesAndNames)
        {
            if (EqualityComparer<TEnum>.Default.Equals(member.Value, default))
            {
                return _zero = member.ExplicitName ?? member.Name;
            }
        }

        return _zero ??= "0";
    }
}
