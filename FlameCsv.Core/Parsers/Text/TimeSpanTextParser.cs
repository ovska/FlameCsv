using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class TimeSpanTextParser : ParserBase<char, TimeSpan>
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

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static TimeSpanTextParser Instance { get; } = new TimeSpanTextParser();

    internal static TimeSpanTextParser GetOrCreate(
        string? format,
        TimeSpanStyles styles,
        IFormatProvider? formatProvider)
    {
        if (format is null && styles == TimeSpanStyles.None && formatProvider == CultureInfo.InvariantCulture)
            return Instance;

        return new(format, styles, formatProvider);
    }
}
