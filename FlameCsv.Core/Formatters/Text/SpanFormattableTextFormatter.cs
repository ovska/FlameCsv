using System.Globalization;
using FlameCsv.Extensions;

namespace FlameCsv.Formatters.Text;

public sealed class SpanFormattableTextFormatter<TValue> : ICsvFormatter<char, TValue?>
    where TValue : ISpanFormattable
{
    /// <summary>
    /// Token to write if the value is null. Empty string and null are equivalent.
    /// </summary>
    /// <remarks>
    /// Not used for value types.
    /// </remarks>
    public string? Null { get; }

    /// <summary>
    /// Optional format provider passed to <see cref="ISpanFormattable.TryFormat"/>.
    /// </summary>
    public IFormatProvider? FormatProvider { get; }

    /// <summary>
    /// Optional format passed to <see cref="ISpanFormattable.TryFormat"/>.
    /// </summary>
    public string? Format { get; }

    public SpanFormattableTextFormatter() : this(null, CultureInfo.InvariantCulture, null)
    {
    }

    public SpanFormattableTextFormatter(
        string? nullToken,
        IFormatProvider? formatProvider,
        string? format)
    {
        Null = nullToken;
        FormatProvider = formatProvider;
        Format = format;
    }

    public bool TryFormat(TValue? value, Span<char> destination, out int tokensWritten)
    {
        if (value is not null) // the condition is JITed out for value types
        {
            return value.TryFormat(destination, out tokensWritten, Format, FormatProvider);
        }

        return Null.AsSpan().TryWriteTo(destination, out tokensWritten);
    }

    public bool CanFormat(Type resultType) => resultType == typeof(TValue);
}
