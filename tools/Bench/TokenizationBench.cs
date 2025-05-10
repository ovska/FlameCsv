// ReSharper disable all

using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class TokenizationBench
{
    [Params(true)]
    public bool CRLF { get; set; }

    [Params(false)]
    public bool Alt { get; set; }

    [Params(false)]
    public bool Chars { get; set; }

    private static readonly Meta[] _metaBuffer = new Meta[24 * 65535];
    private static readonly string _chars0LF = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv");
    private static readonly string _chars1LF = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars0LF);
    private static readonly byte[] _bytes1LF = Encoding.UTF8.GetBytes(_chars1LF);
    private static readonly string _chars0CRLF = _chars0LF.ReplaceLineEndings("\r\n");
    private static readonly string _chars1CRLF = _chars1LF.ReplaceLineEndings("\r\n");
    private static readonly byte[] _bytes0CRLF = Encoding.UTF8.GetBytes(_chars0CRLF);
    private static readonly byte[] _bytes1CRLF = Encoding.UTF8.GetBytes(_chars1CRLF);

    private string CharData => Alt ? (CRLF ? _chars1CRLF : _chars1LF) : (CRLF ? _chars0CRLF : _chars0LF);
    private byte[] ByteData => Alt ? (CRLF ? _bytes1CRLF : _bytes1LF) : (CRLF ? _bytes0CRLF : _bytes0LF);

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

    private readonly SimdTokenizer<char, NewlineLF<char, Vec128Char>, Vec128Char> _t128LF = new(_dCharLF);
    private readonly SimdTokenizer<char, NewlineLF<char, Vec256Char>, Vec256Char> _t256LF = new(_dCharLF);
    private readonly SimdTokenizer<char, NewlineLF<char, Vec512Char>, Vec512Char> _t512LF = new(_dCharLF);
    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec128Byte>, Vec128Byte> _t128bLF = new(_dByteLF);
    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec256Byte>, Vec256Byte> _t256bLF = new(_dByteLF);
    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec512Byte>, Vec512Byte> _t512bLF = new(_dByteLF);

    private readonly SimdTokenizer<byte, NewlineCRLF<byte, Vec128Byte>, Vec128Byte> _t128bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<byte, NewlineCRLF<byte, Vec256Byte>, Vec256Byte> _t256bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<byte, NewlineCRLF<byte, Vec512Byte>, Vec512Byte> _t512bCRLF = new(_dByteCRLF);
    private readonly SimdTokenizer<char, NewlineCRLF<char, Vec128Char>, Vec128Char> _t128CRLF = new(_dCharCRLF);
    private readonly SimdTokenizer<char, NewlineCRLF<char, Vec256Char>, Vec256Char> _t256CRLF = new(_dCharCRLF);
    private readonly SimdTokenizer<char, NewlineCRLF<char, Vec512Char>, Vec512Char> _t512CRLF = new(_dCharCRLF);

    // [Benchmark(Baseline = true)]
    // public int V128()
    // {
    //     if (!Vector128.IsHardwareAccelerated)
    //         throw new NotSupportedException();

    //     if (CRLF)
    //     {
    //         return Chars ? _t128CRLF.Tokenize(_metaBuffer, CharData, 0) : _t128bCRLF.Tokenize(_metaBuffer, ByteData, 0);
    //     }

    //     return Chars ? _t128LF.Tokenize(_metaBuffer, CharData, 0) : _t128bLF.Tokenize(_metaBuffer, ByteData, 0);
    // }

    [Benchmark]
    public int V256()
    {
        if (!Vector256.IsHardwareAccelerated)
            throw new NotSupportedException();

        if (CRLF)
        {
            return Chars ? _t256CRLF.Tokenize(_metaBuffer, CharData, 0) : _t256bCRLF.Tokenize(_metaBuffer, ByteData, 0);
        }

        return Chars ? _t256LF.Tokenize(_metaBuffer, CharData, 0) : _t256bLF.Tokenize(_metaBuffer, ByteData, 0);
    }

    // [Benchmark]
    // public int V512()
    // {
    //     if (!Vector512.IsHardwareAccelerated)
    //         throw new NotSupportedException();

    //     if (CRLF)
    //     {
    //         return Chars ? _t512CRLF.Tokenize(_metaBuffer, CharData, 0) : _t512bCRLF.Tokenize(_metaBuffer, ByteData, 0);
    //     }

    //     return Chars ? _t512LF.Tokenize(_metaBuffer, CharData, 0) : _t512bLF.Tokenize(_metaBuffer, ByteData, 0);
    // }
}
