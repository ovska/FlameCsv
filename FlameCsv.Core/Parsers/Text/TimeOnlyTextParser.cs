using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

public sealed class TimeOnlyTextParser : ParserBase<char, TimeOnly>, ICsvParserFactory<char>
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

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static TimeOnlyTextParser Instance { get; } = new();

    internal static TimeOnlyTextParser GetOrCreate(
        string? format,
        DateTimeStyles styles,
        IFormatProvider? formatProvider)
    {
        if (format is null && styles == DateTimeStyles.None && formatProvider == CultureInfo.InvariantCulture)
            return Instance;

        return new(format, styles, formatProvider);
    }

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.TimeOnlyFormat == null && o.DateTimeStyles == DateTimeStyles.None && o.FormatProvider == CultureInfo.InvariantCulture)
            return this;

        return new TimeOnlyTextParser(o.TimeOnlyFormat, o.DateTimeStyles, o.FormatProvider);
    }
}
