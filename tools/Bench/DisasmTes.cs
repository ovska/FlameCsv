#define CRLF
#define AVX2

using System.Text;
using FlameCsv.Reading.Internal;
#if CRLF
using TNewline = FlameCsv.Reading.Internal.NewlineCRLF;
#else
using TNewline = FlameCsv.Reading.Internal.NewlineLF;
#endif

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class DisasmTest
{
    private const CsvNewline _newline =
#if CRLF
    CsvNewline.CRLF;
#else
    CsvNewline.LF;
#endif

    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];
    private static readonly byte[] _quoteBuffer = new byte[24 * 65535];

    private static readonly string _dataChars = File.ReadAllText(
        "Comparisons/Data/65K_Records_Data.csv"
    // "Comparisons/Data/SampleCSVFile_556kb_4x.csv"
    )
#if CRLF
        .ReplaceLineEndings("\r\n")
#endif
    ;
    private static readonly byte[] _dataBytes = Encoding.UTF8.GetBytes(_dataChars);

    private static readonly CsvOptions<byte> _dByteLF = new CsvOptions<byte> { Newline = _newline };

    private static readonly CsvOptions<char> _dCharLF = new CsvOptions<char> { Newline = _newline };

#if AVX2
    private readonly Avx2Tokenizer<byte, TNewline> _t128bLF = new(_dByteLF);
    private readonly Avx2Tokenizer<char, TNewline> _t128LF = new(_dCharLF);
#else
    private readonly SimdTokenizer<char, TNewline> _t128LF = new(_dCharLF);
    private readonly SimdTokenizer<byte, TNewline> _t128bLF = new(_dByteLF);
#endif

    [Benchmark(Baseline = true)]
    public void Bytes()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };
        _ = _t128bLF.Tokenize(dst, 0, _dataBytes);
    }

    [Benchmark]
    public void Chars()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };
        _ = _t128LF.Tokenize(dst, 0, _dataChars);
    }
}
