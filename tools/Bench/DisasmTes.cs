using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class DisasmTest
{
    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];
    private static readonly byte[] _quoteBuffer = new byte[24 * 65535];
    private static readonly string _chars = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv")
        .ReplaceLineEndings("\r\n");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars);

    private static readonly CsvOptions<byte> _dByteCRLF = new CsvOptions<byte> { Newline = CsvNewline.CRLF };

    private static readonly CsvOptions<char> _dCharCRLF = new CsvOptions<char> { Newline = CsvNewline.CRLF };

    private readonly SimdTokenizer<byte, NewlineCRLF> _t128bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<char, NewlineCRLF> _t128CRLF = new(_dCharCRLF);

    [Benchmark(Baseline = true)]
    public void Chars()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };
        _ = _t128CRLF.Tokenize(dst, 0, _chars);
    }

    [Benchmark]
    public void Bytes()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };
        _ = _t128bCRLF.Tokenize(dst, 0, _bytes0LF);
    }
}
