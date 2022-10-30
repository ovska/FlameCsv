using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class TimeSpanTextParser : ParserBase<char, TimeSpan>
{
    public string? Format { get; }
    public IFormatProvider? FormatProvider { get; }
    public TimeSpanStyles Styles { get; }

    public TimeSpanTextParser(
        string? format = null,
        IFormatProvider? formatProvider = null,
        TimeSpanStyles styles = TimeSpanStyles.None)
    {
        Styles = styles;
        Format = format;
        FormatProvider = formatProvider;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out TimeSpan value)
    {
        return Format is null
            ? TimeSpan.TryParse(span, FormatProvider, out value)
            : TimeSpan.TryParseExact(span, Format, FormatProvider, out value);
    }
}
