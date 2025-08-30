// ReSharper disable all
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class TokenizationBench
{
    public enum ParserNewline
    {
        LF,
        LF_With_CRLF,
        CRLF,
    }

    [Params(
        [ /**/
            false,
            // true,
        ]
    )]
    public bool Chars { get; set; }

    [Params(
        [ /**/
            // true,
            false,
        ]
    )]
    public bool Quoted { get; set; }

    [Params(
        [ /**/
            ParserNewline.LF,
            // ParserNewline.LF_With_CRLF,
            // ParserNewline.CRLF,
        ]
    )]
    public ParserNewline Newline { get; set; }

    public bool DataIsCRLF => Newline == ParserNewline.CRLF;
    public bool TokenizerIsLF => Newline == ParserNewline.LF;

    private static readonly uint[] _fieldBuffer = new uint[24 * 65535];
    private static readonly byte[] _quoteBuffer = new byte[24 * 65535];
    private static readonly string _chars0LF = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv");
    private static readonly string _chars1LF = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars0LF);
    private static readonly byte[] _bytes1LF = Encoding.UTF8.GetBytes(_chars1LF);
    private static readonly string _chars0CRLF = _chars0LF.ReplaceLineEndings("\r\n");
    private static readonly string _chars1CRLF = _chars1LF.ReplaceLineEndings("\r\n");
    private static readonly byte[] _bytes0CRLF = Encoding.UTF8.GetBytes(_chars0CRLF);
    private static readonly byte[] _bytes1CRLF = Encoding.UTF8.GetBytes(_chars1CRLF);

    private string CharData => Quoted ? (DataIsCRLF ? _chars1CRLF : _chars1LF) : (DataIsCRLF ? _chars0CRLF : _chars0LF);

    private byte[] ByteData => Quoted ? (DataIsCRLF ? _bytes1CRLF : _bytes1LF) : (DataIsCRLF ? _bytes0CRLF : _bytes0LF);

    private static readonly CsvOptions<char> _dCharLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<byte> _dByteCRLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private static readonly CsvOptions<byte> _dByteLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<char> _dCharCRLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private readonly SimdTokenizer<char, NewlineLF> _t128LF = new(_dCharLF);
    private readonly SimdTokenizer<byte, NewlineLF> _t128bLF = new(_dByteLF);
    private readonly SimdTokenizer<byte, NewlineCRLF> _t128bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<char, NewlineCRLF> _t128CRLF = new(_dCharCRLF);

    private CsvPartialTokenizer<char> SimdChar => TokenizerIsLF ? _t128LF : _t128CRLF;
    private CsvPartialTokenizer<byte> SimdByte => TokenizerIsLF ? _t128bLF : _t128bCRLF;

    // [Benchmark(Baseline = true)]
    public void Simd()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };

        if (Chars)
        {
            _ = SimdChar.Tokenize(dst, 0, CharData);
        }
        else
        {
            _ = SimdByte.Tokenize(dst, 0, ByteData);
        }
    }

    private readonly Avx2Tokenizer<byte, NewlineLF> _avx2Byte = new(_dByteLF);
    private readonly Avx2Tokenizer<char, NewlineLF> _avx2Char = new(_dCharLF);
    private readonly Avx2Tokenizer<byte, NewlineCRLF> _avx2ByteCRLF = new(_dByteCRLF);
    private readonly Avx2Tokenizer<char, NewlineCRLF> _avx2CharCRLF = new(_dCharCRLF);

    private CsvPartialTokenizer<char> Avx2Char => TokenizerIsLF ? _avx2Char : _avx2CharCRLF;
    private CsvPartialTokenizer<byte> Avx2Byte => TokenizerIsLF ? _avx2Byte : _avx2ByteCRLF;

    [Benchmark]
    public void Avx2()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };

        if (Chars)
        {
            _ = Avx2Char.Tokenize(dst, 0, CharData);
        }
        else
        {
            _ = Avx2Byte.Tokenize(dst, 0, ByteData);
        }
    }

#if NET10_0_OR_GREATER
    private readonly Avx512Tokenizer<byte, NewlineLF> _avx512Byte = new(_dByteLF);
    private readonly Avx512Tokenizer<char, NewlineLF> _avx512Char = new(_dCharLF);
    private readonly Avx512Tokenizer<byte, NewlineCRLF> _avx512ByteCRLF = new(_dByteCRLF);
    private readonly Avx512Tokenizer<char, NewlineCRLF> _avx512CharCRLF = new(_dCharCRLF);

    private CsvPartialTokenizer<char> Avx512Char => TokenizerIsLF ? _avx512Char : _avx512CharCRLF;
    private CsvPartialTokenizer<byte> Avx512Byte => TokenizerIsLF ? _avx512Byte : _avx512ByteCRLF;

    // [Benchmark]
    public void Avx512()
    {
        var dst = new FieldBuffer { Fields = _fieldBuffer, Quotes = _quoteBuffer };

        if (Chars)
        {
            _ = Avx512Char.Tokenize(dst, 0, CharData);
        }
        else
        {
            _ = Avx512Byte.Tokenize(dst, 0, ByteData);
        }
    }
#endif
}
