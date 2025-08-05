#if false
// ReSharper disable all
using System.Text;
using CommunityToolkit.HighPerformance;
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

    // [Params(false, true)]
    public bool Chars { get; set; }

    // [Params(true, false)]
    public bool Quoted { get; set; } = true;

    [Params(
        [
            /**/
            ParserNewline.LF,
            // ParserNewline.LF_With_CRLF,
            ParserNewline.CRLF,
        ]
    )]
    public ParserNewline Newline { get; set; }

    public bool DataIsCRLF => Newline == ParserNewline.CRLF;
    public bool TokenizerIsLF => Newline == ParserNewline.LF;

    private static readonly int[] _eolBuffer = new int[24 * 65535];
    private static readonly uint[] _flagBuffer = new uint[24 * 65535];
    private static readonly Meta[] _metaBuffer = new Meta[24 * 65535];
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

    // [Benchmark(Baseline = true)]
    public void V128()
    {
        var rb = new RecordBuffer();
        rb.GetFieldArrayRef() = _metaBuffer;
        rb.UnsafeGetEOLArrayRef() = _eolBuffer;

        if (Chars)
        {
            CsvPartialTokenizer<char> tokenizer = TokenizerIsLF ? _t128LF : _t128CRLF;
            _ = tokenizer.Tokenize(rb, CharData);
        }
        else
        {
            if (!TokenizerIsLF)
                throw new Exception();

            CsvPartialTokenizer<byte> tokenizer = TokenizerIsLF ? _t128bLF : _t128bCRLF;
            _ = tokenizer.Tokenize(rb, ByteData);
        }
    }

    private readonly Avx512Tokenizer<byte, NewlineLF> _avxByte = new(_dByteLF);
    private readonly Avx512Tokenizer<char, NewlineLF> _avxChar = new(_dCharLF);
    private readonly Avx512Tokenizer<byte, NewlineCRLF> _avxByteCRLF = new(_dByteCRLF);
    private readonly Avx512Tokenizer<char, NewlineCRLF> _avxCharCRLF = new(_dCharCRLF);

    [Benchmark]
    public void Avx512()
    {
        if (Chars)
        {
            if (!TokenizerIsLF)
            {
                _avxCharCRLF.Tokenize(_eolBuffer, _flagBuffer.AsSpan(), CharData);
            }
            else
            {
                _avxChar.Tokenize(_eolBuffer, _flagBuffer.AsSpan(), CharData);
            }
        }
        else
        {
            if (!TokenizerIsLF)
            {
                _avxByteCRLF.Tokenize(_eolBuffer, _flagBuffer.AsSpan(), ByteData);
            }
            else
            {
                _avxByte.Tokenize(_eolBuffer, _flagBuffer.AsSpan(), ByteData);
            }
        }
    }

    // [Benchmark]
    // public void V512()
    // {
    //     var rb = new RecordBuffer();
    //     rb.UnsafeGetArrayRef() = _metaBuffer;
    //     rb.UnsafeGetEOLArrayRef() = _eolBuffer;

    //     if (Chars)
    //     {
    //         AltTokenizerBase<char> tokenizer = TokenizerIsLF ? _t512LF : _t512CRLF;
    //         _ = tokenizer.Tokenize(rb, CharData);
    //     }
    //     else
    //     {
    //         AltTokenizerBase<byte> tokenizer = TokenizerIsLF ? _t512bLF : _t512bCRLF;
    //         _ = tokenizer.Tokenize(rb, ByteData);
    //     }
    // }
}
#endif
