namespace FlameCsv.Parsers.Text;

public sealed class GuidTextParser : ParserBase<char, Guid>
{
    /// <summary>
    /// Guid format. If not null, exact parsing is used.
    /// </summary>
    public string? Format { get; }

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
