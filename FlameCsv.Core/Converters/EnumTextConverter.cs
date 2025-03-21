namespace FlameCsv.Converters;

/// <summary>
/// The default converter for non-flags enums.
/// </summary>
internal sealed class EnumTextConverter<TEnum> : CsvConverter<char, TEnum>
    where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly IFormatProvider? _formatProvider;

    /// <summary>
    /// Creates a new enum converter.
    /// </summary>
    public EnumTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _formatProvider = options.GetFormatProvider(typeof(TEnum));
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);
    }

    /// <inheritdoc/>
    public override bool TryFormat(Span<char> destination, TEnum value, out int charsWritten)
    {
        return ((ISpanFormattable)value).TryFormat(destination, out charsWritten, _format, _formatProvider);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> source, out TEnum value)
    {
        return Enum.TryParse(source, _ignoreCase, out value) && (_allowUndefinedValues || Enum.IsDefined(value));
    }
}
