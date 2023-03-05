namespace FlameCsv.Parsers.Text;

public sealed class GuidTextParser : ParserBase<char, Guid>
{
    /// <summary>
    /// Guid format. If not null, exact parsing is used.
    /// </summary>
    public string? Format { get; }

    /// <summary>
    /// Initializes an instance of <see cref="GuidTextParser"/> using the specified format.
    /// </summary>
    /// <param name="format">
    /// Format used with <see cref="Guid.TryParseExact(ReadOnlySpan{char}, ReadOnlySpan{char}, out Guid)"/>.
    /// If null, <see cref="Guid.TryParse(ReadOnlySpan{char}, out Guid)"/> is used
    /// </param>
    public GuidTextParser(string? format = null)
    {
        Format = format;
    }

    public override bool TryParse(ReadOnlySpan<char> span, out Guid value)
    {
        return Format is null
            ? Guid.TryParse(span, out value)
            : Guid.TryParseExact(span, Format.AsSpan(), out value);
    }
}
