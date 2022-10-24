using System.Buffers.Text;

namespace FlameCsv.Parsers.Utf8;

public sealed class IntegerUtf8Parser :
    ICsvParser<byte, byte>,
    ICsvParser<byte, short>,
    ICsvParser<byte, ushort>,
    ICsvParser<byte, int>,
    ICsvParser<byte, uint>,
    ICsvParser<byte, long>,
    ICsvParser<byte, ulong>,
    ICsvParser<byte, nint>,
    ICsvParser<byte, nuint>
{
    /// <summary>
    /// Format parameter passed to <see cref="Utf8Parser"/>.
    /// </summary>
    public char StandardFormat { get; }

    public IntegerUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out int _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out byte value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out short value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ushort value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out int value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out uint value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out long value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out ulong value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out nint value)
    {
        if (TryParse(span, out long _value))
        {
            value = (nint)_value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out nuint value)
    {
        if (TryParse(span, out ulong _value))
        {
            value = (nuint)_value;
            return true;
        }

        value = default;
        return false;
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
