using System.Runtime.CompilerServices;
using FlameCsv.Reflection;
using FlameCsv.Utilities;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Internal implementation detail, used by the source generator.
/// </summary>
public sealed class CsvEnumFlagsTextFormatStrategy<TEnum> : CsvEnumFlagsFormatStrategy<char, TEnum>
    where TEnum : struct, Enum
{
    // ReSharper disable once StaticMemberInGenericType
    private static string? _zero;

    static CsvEnumFlagsTextFormatStrategy()
    {
        HotReloadService.RegisterForHotReload(
            typeof(TEnum),
            static _ => _zero = null);
    }

    /// <inheritdoc />
    public CsvEnumFlagsTextFormatStrategy(CsvOptions<char> options, EnumFormatStrategy<char, TEnum> inner)
        : base(options, inner)
    {
    }

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
