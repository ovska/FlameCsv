using System.Buffers.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

public sealed class DecimalUtf8Parser :
    ICsvParser<byte, double>,
    ICsvParser<byte, float>,
    ICsvParser<byte, Half>,
    ICsvParser<byte, decimal>,
    ICsvParserFactory<byte>
{
    /// <summary>
    /// Format parameter passed to <see cref="Utf8Parser"/>.
    /// </summary>
    public char StandardFormat { get; }

    public DecimalUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out double _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out double value)
    {
        return Utf8Parser.TryParse(span, out value, out int _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out float value)
    {
        return Utf8Parser.TryParse(span, out value, out int _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out Half value)
    {
        if (Utf8Parser.TryParse(span, out double _value, out int _, StandardFormat))
        {
            value = (Half)_value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out decimal value)
    {
        return Utf8Parser.TryParse(span, out value, out int _, StandardFormat);
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(double)
            || resultType == typeof(float)
            || resultType == typeof(Half)
            || resultType == typeof(decimal);
    }

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        var o = GuardEx.IsType<CsvUtf8ReaderOptions>(options);
        return o.DecimalFormat == default ? this : new DecimalUtf8Parser(o.DecimalFormat);
    }
}
