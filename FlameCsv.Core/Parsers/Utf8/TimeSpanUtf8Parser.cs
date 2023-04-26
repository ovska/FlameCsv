using System.Buffers.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

public sealed class TimeSpanUtf8Parser : ParserBase<byte, TimeSpan>, ICsvParserFactory<byte>
{
    public char StandardFormat { get; }

    public TimeSpanUtf8Parser(char standardFormat = '\0')
    {
        // validate the parameter
        _ = Utf8Parser.TryParse(default, out TimeSpan _, out _, standardFormat);

        StandardFormat = standardFormat;
    }

    public override bool TryParse(ReadOnlySpan<byte> span, out TimeSpan value)
    {
        return Utf8Parser.TryParse(span, out value, out _, StandardFormat);
    }

    ICsvParser<byte> ICsvParserFactory<byte>.Create(Type resultType, CsvReaderOptions<byte> options)
    {
        var o = GuardEx.IsType<CsvUtf8ReaderOptions>(options);
        return o.TimeSpanFormat == default ? this : new TimeSpanUtf8Parser(o.TimeSpanFormat);
    }
}
