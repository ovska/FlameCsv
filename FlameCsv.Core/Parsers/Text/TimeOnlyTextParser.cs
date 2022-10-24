using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class TimeOnlyTextParser : ParserBase<char, TimeOnly>
{
    public string? Format { get; }
    public DateTimeStyles Styles { get; }
    public IFormatProvider? FormatProvider { get; }

    public TimeOnlyTextParser(
        string? format = null,
        DateTimeStyles styles = default,
        IFormatProvider? formatProvider = null)
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
