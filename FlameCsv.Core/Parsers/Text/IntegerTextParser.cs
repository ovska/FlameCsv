using System.Globalization;

namespace FlameCsv.Parsers.Text;

public sealed class IntegerTextParser :
    ICsvParser<char, byte>,
    ICsvParser<char, short>,
    ICsvParser<char, ushort>,
    ICsvParser<char, int>,
    ICsvParser<char, uint>,
    ICsvParser<char, long>,
    ICsvParser<char, ulong>,
    ICsvParser<char, nint>,
    ICsvParser<char, nuint>
{
    public NumberStyles Styles { get; }
    public IFormatProvider? FormatProvider { get; }

    public IntegerTextParser() : this(NumberStyles.Integer)
    {
    }

    public IntegerTextParser(
        NumberStyles styles = NumberStyles.Integer,
        IFormatProvider? formatProvider = null)
    {
        Styles = styles;
        FormatProvider = formatProvider;
    }

    public bool TryParse(ReadOnlySpan<char> span, out byte value)
    {
        return byte.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out short value)
    {
        return short.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out ushort value)
    {
        return ushort.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out int value)
    {
        return int.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out uint value)
    {
        return uint.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out long value)
    {
        return long.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out ulong value)
    {
        return ulong.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out nint value)
    {
        return nint.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool TryParse(ReadOnlySpan<char> span, out nuint value)
    {
        return nuint.TryParse(span, Styles, FormatProvider, out value);
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(byte)
            || resultType == typeof(short)
            || resultType == typeof(ushort)
            || resultType == typeof(int)
            || resultType == typeof(uint)
            || resultType == typeof(long)
            || resultType == typeof(ulong)
            || resultType == typeof(nint)
            || resultType == typeof(nuint);
    }
}
