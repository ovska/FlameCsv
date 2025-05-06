using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reflection;
using FlameCsv.Utilities;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Internal implementation detail, used by the source generator.
/// </summary>
public sealed class CsvEnumFlagsUtf8FormatStrategy<TEnum> : CsvEnumFlagsFormatStrategy<byte, TEnum>
    where TEnum : struct, Enum
{
    // ReSharper disable once StaticMemberInGenericType
    private static byte[]? _zero;

    static CsvEnumFlagsUtf8FormatStrategy()
    {
        HotReloadService.RegisterForHotReload(
            typeof(TEnum),
            static _ => _zero = null);
    }

    /// <inheritdoc />
    public CsvEnumFlagsUtf8FormatStrategy(CsvOptions<byte> options, EnumFormatStrategy<byte, TEnum> inner)
        : base(options, inner)
    {
    }

    /// <inheritdoc />
    protected override ReadOnlySpan<byte> Zero => _zero ?? InitSharedZero();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte[] InitSharedZero()
    {
        foreach (var member in EnumMemberCache<TEnum>.ValuesAndNames)
        {
            if (EqualityComparer<TEnum>.Default.Equals(member.Value, default))
            {
                return _zero = Encoding.UTF8.GetBytes(member.ExplicitName ?? member.Name);
            }
        }

        return _zero ??= SharedZero.Value;
    }
}

// move out of generic class
file static class SharedZero
{
    public static readonly byte[] Value = [(byte)'0'];
}
