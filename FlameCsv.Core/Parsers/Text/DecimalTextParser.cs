using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class DecimalTextParser :
    ICsvParser<char, double>,
    ICsvParser<char, float>,
    ICsvParser<char, Half>,
    ICsvParser<char, decimal>
{
    public NumberStyles Styles { get; }
    public IFormatProvider? FormatProvider { get; }

    public DecimalTextParser(
        NumberStyles styles = NumberStyles.Integer,
        IFormatProvider? formatProvider = null)
    {
        Styles = styles;
        FormatProvider = formatProvider;
    }

    public bool TryParse(ReadOnlySpan<char> span, out double value)
    {
        return double.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out float value)
    {
        return float.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out Half value)
    {
        return Half.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out decimal value)
    {
        return decimal.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(double)
            || resultType == typeof(float)
            || resultType == typeof(Half)
            || resultType == typeof(decimal);
    }
}
