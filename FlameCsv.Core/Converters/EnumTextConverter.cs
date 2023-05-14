using FlameCsv.Extensions;

namespace FlameCsv.Converters.Text;

/// <summary>
/// Parser for non-flags enums.
/// </summary>
internal sealed class EnumTextConverter<TEnum> : CsvConverter<char, TEnum>
    where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly Dictionary<TEnum, string> _values;

    public EnumTextConverter(CsvTextOptions? options)
    {
        _allowUndefinedValues = options?.AllowUndefinedEnumValues ?? false;
        _ignoreCase = options?.IgnoreEnumCase ?? true;

        _values = new Dictionary<TEnum, string>();

        foreach (var value in Enum.GetValues<TEnum>())
        {
            _values.TryAdd(value, value.ToString());
        }
    }

    public override bool TryFormat(Span<char> buffer, TEnum value, out int charsWritten)
    {
        if (!_values.TryGetValue(value, out string? name))
        {
            name = value.ToString();
        }

        return name.AsSpan().TryWriteTo(buffer, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> span, out TEnum value)
    {
        return Enum.TryParse(span, _ignoreCase, out value) && (_allowUndefinedValues || Enum.IsDefined(value));
    }
}
