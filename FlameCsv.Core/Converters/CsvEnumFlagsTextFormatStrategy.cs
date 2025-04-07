using FlameCsv.Utilities;

namespace FlameCsv.Converters;

/// <summary>
/// Internal implementation detail, used by the source generator.
/// </summary>
public sealed class CsvEnumFlagsTextFormatStrategy<TEnum> : CsvEnumFlagsFormatStrategy<char, TEnum>
    where TEnum : struct, Enum
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly string _zero;

    static CsvEnumFlagsTextFormatStrategy()
    {
        foreach (var member in EnumMemberCache<TEnum>.ValuesAndNames)
        {
            if (EqualityComparer<TEnum>.Default.Equals(member.Value, default))
            {
                _zero ??= member.ExplicitName ?? member.Name;
                break;
            }
        }

        _zero ??= "0";
        // TODO: hot reload support?
    }

    /// <inheritdoc />
    public CsvEnumFlagsTextFormatStrategy(CsvOptions<char> options, EnumFormatStrategy<char, TEnum> inner) : base(
        options,
        inner)
    {
    }

    /// <inheritdoc />
    protected override ReadOnlySpan<char> Zero => _zero;
}
