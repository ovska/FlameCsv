using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Parser for <see langword="byte"/>, <see langword="sbyte"/>, <see langword="short"/>, <see langword="ushort"/>,
/// <see langword="int"/>, <see langword="uint"/>, <see langword="long"/>, <see langword="ulong"/>,
/// <see langword="nint"/>, and <see langword="nuint"/>.
/// </summary>
public sealed class IntegerTextParser :
    ICsvParser<char, sbyte>,
    ICsvParser<char, byte>,
    ICsvParser<char, short>,
    ICsvParser<char, ushort>,
    ICsvParser<char, int>,
    ICsvParser<char, uint>,
    ICsvParser<char, long>,
    ICsvParser<char, ulong>,
    ICsvParser<char, nint>,
    ICsvParser<char, nuint>,
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
    /// Initializes a new <see cref="IntegerTextParser"/> using <see cref="NumberStyles.Integer"/>
    /// and <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IntegerTextParser() : this(CultureInfo.InvariantCulture, NumberStyles.Integer)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="IntegerTextParser"/> using the specified number styles
    /// and <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IntegerTextParser(NumberStyles styles) : this(CultureInfo.InvariantCulture, styles)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="IntegerTextParser"/> using the specified number styles and format provider.
    /// </summary>
    public IntegerTextParser(
        IFormatProvider? formatProvider,
        NumberStyles styles = NumberStyles.Integer)
    {
        Styles = styles;
        FormatProvider = formatProvider;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out sbyte value)
    {
        return sbyte.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out byte value)
    {
        return byte.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out short value)
    {
        return short.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ushort value)
    {
        return ushort.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out int value)
    {
        return int.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out uint value)
    {
        return uint.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out long value)
    {
        return long.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ulong value)
    {
        return ulong.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out nint value)
    {
        return nint.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out nuint value)
    {
        return nuint.TryParse(span, Styles, FormatProvider, out value);
    }

    /// <inheritdoc/>
    public bool CanParse(Type resultType)
    {
        return resultType == typeof(byte)
            || resultType == typeof(sbyte)
            || resultType == typeof(short)
            || resultType == typeof(ushort)
            || resultType == typeof(int)
            || resultType == typeof(uint)
            || resultType == typeof(long)
            || resultType == typeof(ulong)
            || resultType == typeof(nint)
            || resultType == typeof(nuint);
    }

    /// <summary>Thread-safe singleton instance initialized to default values.</summary>
    public static IntegerTextParser Instance { get; } = new();

    internal static IntegerTextParser GetOrCreate(
        IFormatProvider? formatProvider,
        NumberStyles styles)
    {
        if (styles == NumberStyles.Integer && formatProvider == CultureInfo.InvariantCulture)
            return Instance;

        return new(formatProvider, styles);
    }

    ICsvParser<char> ICsvParserFactory<char>.Create(Type resultType, CsvReaderOptions<char> options)
    {
        var o = GuardEx.IsType<CsvTextReaderOptions>(options);

        if (o.IntegerNumberStyles == NumberStyles.Integer && o.FormatProvider == CultureInfo.InvariantCulture)
            return this;

        return new IntegerTextParser(o.FormatProvider, o.IntegerNumberStyles);
    }
}
