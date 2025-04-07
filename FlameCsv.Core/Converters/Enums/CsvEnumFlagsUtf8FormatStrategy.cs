using System.Text;
using FlameCsv.Utilities;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Internal implementation detail, used by the source generator.
/// </summary>
public sealed class CsvEnumFlagsUtf8FormatStrategy<TEnum> : CsvEnumFlagsFormatStrategy<byte, TEnum>
    where TEnum : struct, Enum
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly byte[] _zero;

    static CsvEnumFlagsUtf8FormatStrategy()
    {
        foreach (var member in EnumMemberCache<TEnum>.ValuesAndNames)
        {
            if (EqualityComparer<TEnum>.Default.Equals(member.Value, default))
            {
                _zero ??= Encoding.UTF8.GetBytes(member.ExplicitName ?? member.Name);
                return;
            }
        }

        _zero = [(byte)'\0'];
        // TODO: hot reload support?
    }

    /// <inheritdoc />
    public CsvEnumFlagsUtf8FormatStrategy(CsvOptions<byte> options, EnumFormatStrategy<byte, TEnum> inner) : base(
        options,
        inner)
    {
    }

    /// <inheritdoc />
    protected override ReadOnlySpan<byte> Zero => _zero;
}
