using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see langword="double"/>, <see langword="float"/>, <see langword="decimal"/> and <see cref="Half"/>.
/// </summary>
public sealed class DecimalTextParser :
    ICsvParser<char, double>,
    ICsvParser<char, float>,
    ICsvParser<char, Half>,
    ICsvParser<char, decimal>,
    ICsvParserFactory<char>
{
    /// <summary>
    /// Number styles passed to <c>TryParse</c>.
    /// </summary>
    public NumberStyles Styles { get; }

    /// <summary>
    /// Format provider passed to <c>TryParse</c>.
    /// </summary>
    public IFormatProvider? FormatProvider { get; }

    /// <summary>
    /// Initializes a new <see cref="DecimalTextParser"/> using <see cref="NumberStyles.Float"/>
    /// and <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public DecimalTextParser() : this(CultureInfo.InvariantCulture)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="DecimalTextParser"/> using the specified number styles
    /// and <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public DecimalTextParser(NumberStyles styles) : this(CultureInfo.InvariantCulture, styles)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="DecimalTextParser"/>.
    /// </summary>
    public DecimalTextParser(
        IFormatProvider? formatProvider,
        NumberStyles styles = NumberStyles.Float)
    {
        Styles = styles;
        FormatProvider = formatProvider;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out double value)
    {
        return double.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out float value)
    {
        return float.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out Half value)
    {
        return Half.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out decimal value)
    {
        return decimal.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool CanParse(Type resultType)
    {
        return resultType == typeof(double)
            || resultType == typeof(float)
            || resultType == typeof(Half)
            || resultType == typeof(decimal);
    }

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.DecimalNumberStyles == NumberStyles.Float && o.FormatProvider == CultureInfo.InvariantCulture)
            return this;

        return new DecimalTextParser(o.FormatProvider, o.DecimalNumberStyles);
    }
}
