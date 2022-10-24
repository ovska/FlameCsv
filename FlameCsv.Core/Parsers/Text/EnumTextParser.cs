namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for non-flags enums.
/// </summary>
public sealed class EnumTextParser<TEnum> : ICsvParser<char, TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Whether parsed values should be validated to be defined.
    /// </summary>
    public bool AllowUndefinedValues { get; }

    /// <summary>
    /// Whether case should be ignored.
    /// </summary>
    public bool IgnoreCase { get; }

    public EnumTextParser(
        bool allowUndefinedValues = false,
        bool ignoreCase = true)
    {
        AllowUndefinedValues = allowUndefinedValues;
        IgnoreCase = ignoreCase;
    }

    public bool TryParse(ReadOnlySpan<char> span, out TEnum value)
    {
        return Enum.TryParse(span, IgnoreCase, out value) && (AllowUndefinedValues || Enum.IsDefined(value));
    }

    public bool CanParse(Type resultType) => resultType == typeof(TEnum);
}
