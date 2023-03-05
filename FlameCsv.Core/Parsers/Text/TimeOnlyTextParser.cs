using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class TimeOnlyTextParser : ParserBase<char, TimeOnly>
{
    public string? Format { get; }
    public DateTimeStyles Styles { get; }
    public IFormatProvider? FormatProvider { get; }

    public TimeOnlyTextParser() : this(null, DateTimeStyles.None, CultureInfo.InvariantCulture)
    {
    }

    public TimeOnlyTextParser(string? format) : this(format, DateTimeStyles.None, CultureInfo.InvariantCulture)
    {
    }

    public TimeOnlyTextParser(
        string? format,
        DateTimeStyles styles,
        IFormatProvider? formatProvider)
    {
        Styles = styles;
        Format = format;
        FormatProvider = formatProvider;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out TimeOnly value)
    {
        return Format is null
            ? TimeOnly.TryParse(span, FormatProvider, Styles, out value)
            : TimeOnly.TryParseExact(span, Format, FormatProvider, Styles, out value);
    }
}
