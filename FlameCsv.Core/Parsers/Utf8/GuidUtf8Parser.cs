using System.Buffers.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

public class GuidUtf8Parser : ParserBase<byte, Guid>, ICsvParserFactory<byte>
{
    public char StandardFormat { get; }

    public GuidUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out Guid _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    public override bool TryParse(ReadOnlySpan<byte> span, out Guid value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        var o = GuardEx.IsType<CsvUtf8ReaderOptions>(options);
        return o.GuidFormat == default ? this : new GuidUtf8Parser(o.GuidFormat);
    }
}
