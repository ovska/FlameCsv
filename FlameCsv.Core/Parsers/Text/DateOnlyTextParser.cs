using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see cref="DateOnly"/>.
/// </summary>
public sealed class DateOnlyTextParser : ParserBase<char, DateOnly>, ICsvParserFactory<char>
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

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.DateTimeFormat == null && o.DateTimeStyles == DateTimeStyles.None && o.FormatProvider == CultureInfo.InvariantCulture)
            return this;

        return new DateOnlyTextParser(o.DateTimeFormat, o.DateTimeStyles, o.FormatProvider);
    }
}
