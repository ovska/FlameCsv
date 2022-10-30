using FlameCsv.Extensions;
using FlameCsv.Writers;

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

    public SpanFormattableTextFormatter(
        string? nullToken,
        IFormatProvider? formatProvider = null,
        string? format = null)
    {
        Null = nullToken;
        FormatProvider = formatProvider;
        Format = format;
    }

    public bool TryFormat(TValue? value, Span<char> buffer, out int tokensWritten)
    {
        if (value is not null) // the condition is JITed out for value types
        {
            return value.TryFormat(buffer, out tokensWritten, Format.AsSpan(), FormatProvider);
        }

        return Null.AsSpan().TryWriteTo(buffer, out tokensWritten);
    }
}
