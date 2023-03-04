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

    /// <summary>
    /// Initializes an instance of <see cref="DateOnlyTextParser"/>.
    /// </summary>
    public DateOnlyTextParser(
        string? format = null,
        DateTimeStyles styles = default,
        IFormatProvider? formatProvider = null)
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
}
