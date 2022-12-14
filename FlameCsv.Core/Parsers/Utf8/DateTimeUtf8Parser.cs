using System.Buffers.Text;

namespace FlameCsv.Parsers.Utf8;

public sealed class DateTimeUtf8Parser :
    ICsvParser<byte, DateTime>,
    ICsvParser<byte, DateTimeOffset>
{
    public char StandardFormat { get; }

    public DateTimeUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out DateTime _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out DateTime value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool TryParse(ReadOnlySpan<byte> span, out DateTimeOffset value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(DateTime) || resultType == typeof(DateTimeOffset);
    }
}
