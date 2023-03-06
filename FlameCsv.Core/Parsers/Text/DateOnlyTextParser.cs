using System.Globalization;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see cref="DateOnly"/>.
/// </summary>
public sealed class DateOnlyTextParser : ParserBase<char, DateOnly>
{
    /// <summary>
    /// Format used.
    /// </summary>
    public string? Format { get; }

    /// <summary>
    /// DateTimeStyles used.
    /// </summary>
    public DateTimeStyles Styles { get; }

    /// <summary>
    /// Format provider used.
    /// </summary>
    public IFormatProvider? FormatProvider { get; }

    public DateOnlyTextParser() : this(null, DateTimeStyles.None, CultureInfo.InvariantCulture)
    {
    }

    /// <summary>
    /// Initializes an instance of <see cref="DateOnlyTextParser"/>.
    /// </summary>
    public DateOnlyTextParser(
        string? format,
        DateTimeStyles styles,
        IFormatProvider? formatProvider)
    {
        Styles = styles;
        Format = format;
        FormatProvider = formatProvider;
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> span, out DateOnly value)
    {
        return Format is null
            ? DateOnly.TryParse(span, FormatProvider, Styles, out value)
            : DateOnly.TryParseExact(span, Format, FormatProvider, Styles, out value);
    }

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static DateOnlyTextParser Instance { get; } = new DateOnlyTextParser();

    internal static DateOnlyTextParser GetOrCreate(
        string? format,
        DateTimeStyles styles,
        IFormatProvider? formatProvider)
    {
        if (format is null && styles == DateTimeStyles.None && formatProvider == CultureInfo.InvariantCulture)
            return Instance;

        return new(format, styles, formatProvider);
    }
}
