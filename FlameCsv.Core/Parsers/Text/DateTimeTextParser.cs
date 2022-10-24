using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class DateTimeTextParser :
    ICsvParser<char, DateTime>,
    ICsvParser<char, DateTimeOffset>
{
    public string? Format { get; }
    public IFormatProvider? FormatProvider { get; }
    public DateTimeStyles Styles { get; }

    public DateTimeTextParser(
        string? format = null,
        IFormatProvider? formatProvider = null,
        DateTimeStyles styles = DateTimeStyles.None)
    {
        Format = format;
        FormatProvider = formatProvider;
        Styles = styles;
    }

    public bool TryParse(ReadOnlySpan<char> span, out DateTime value)
    {
        return Format is null
            ? DateTime.TryParse(span, FormatProvider, Styles, out value)
            : DateTime.TryParseExact(span, Format, FormatProvider, Styles, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out DateTimeOffset value)
    {
        return Format is null
            ? DateTimeOffset.TryParse(span, FormatProvider, Styles, out value)
            : DateTimeOffset.TryParseExact(span, Format, FormatProvider, Styles, out value);
    }

    public bool CanParse(Type resultType) => resultType == typeof(DateTime) || resultType == typeof(DateTimeOffset);
}
