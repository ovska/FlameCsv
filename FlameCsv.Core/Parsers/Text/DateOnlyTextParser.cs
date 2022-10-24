using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class DateOnlyTextParser : ParserBase<char, DateOnly>
{
    public string? Format { get; }
    public DateTimeStyles Styles { get; }
    public IFormatProvider? FormatProvider { get; }

    public DateOnlyTextParser(
        string? format = null,
        DateTimeStyles styles = default,
        IFormatProvider? formatProvider = null)
    {
        Styles = styles;
        Format = format;
        FormatProvider = formatProvider;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out DateOnly value)
    {
        return Format is null
            ? DateOnly.TryParse(span, FormatProvider, Styles, out value)
            : DateOnly.TryParseExact(span, Format, FormatProvider, Styles, out value);
    }
}
