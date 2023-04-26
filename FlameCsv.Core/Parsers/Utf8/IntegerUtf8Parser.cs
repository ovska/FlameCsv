using System.Buffers.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Parser for <see langword="byte"/>, <see langword="sbyte"/>, <see langword="short"/>, <see langword="ushort"/>,
/// <see langword="int"/>, <see langword="uint"/>, <see langword="long"/>, <see langword="ulong"/>,
/// <see langword="nint"/>, and <see langword="nuint"/>.
/// </summary>
public sealed class IntegerUtf8Parser :
    ICsvParser<byte, byte>,
    ICsvParser<byte, short>,
    ICsvParser<byte, ushort>,
    ICsvParser<byte, int>,
    ICsvParser<byte, uint>,
    ICsvParser<byte, long>,
    ICsvParser<byte, ulong>,
    ICsvParser<byte, nint>,
    ICsvParser<byte, nuint>,
    ICsvParserFactory<byte>
{
    /// <summary>
    /// Format parameter passed to <see cref="Utf8Parser"/>.
    /// </summary>
    public char StandardFormat { get; }

    /// <summary>
    /// Initializes a new <see cref="IntegerUtf8Parser"/> using the specified format.
    /// </summary>
    public IntegerUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out int _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out byte value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out short value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out ushort value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out int value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out uint value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out long value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<byte> span, out ulong value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        var o = GuardEx.IsType<CsvUtf8ReaderOptions>(options);
        return o.IntegerFormat == default ? this : new IntegerUtf8Parser(o.IntegerFormat);
    }
}
