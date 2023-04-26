using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

public sealed class TimeSpanTextParser : ParserBase<char, TimeSpan>, ICsvParserFactory<char>
{
    public string? Format { get; }
    public IFormatProvider? FormatProvider { get; }
    public TimeSpanStyles Styles { get; }

    public TimeSpanTextParser() : this(null, TimeSpanStyles.None, CultureInfo.InvariantCulture)
    {
    }

    public TimeSpanTextParser(string? format) : this(format, TimeSpanStyles.None, CultureInfo.InvariantCulture)
    {
    }

    public TimeSpanTextParser(
        string? format,
        TimeSpanStyles styles,
        IFormatProvider? formatProvider)
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

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.TimeSpanFormat == null && o.TimeSpanStyles == TimeSpanStyles.None && o.FormatProvider == CultureInfo.InvariantCulture)
            return this;

        return new TimeSpanTextParser(o.TimeSpanFormat, o.TimeSpanStyles, o.FormatProvider);
    }
}
