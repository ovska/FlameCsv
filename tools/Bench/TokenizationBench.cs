// ReSharper disable all

using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Intrinsics;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class TokenizationBench
{
    [Params(true, false)]
    public bool Alt { get; set; }

    [Params(true, false)]
    public bool Chars { get; set; }

    private readonly Meta[] _metaBuffer = new Meta[24 * 65535];
    private readonly string _chars0 = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv", Encoding.UTF8);
    private readonly string _chars1 = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv", Encoding.UTF8);
    private readonly byte[] _bytes0 = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
    private readonly byte[] _bytes1 = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb_4x.csv");

    private string CharData => Alt ? _chars1 : _chars0;
    private byte[] ByteData => Alt ? _bytes1 : _bytes0;

    private static readonly CsvOptions<char> _dialectChar = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<byte> _dialectByte = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private readonly SimdTokenizer<char, NewlineLF<char, Vec128Char>, Vec128Char> _t128 = new(_dialectChar);

    private readonly SimdTokenizer<char, NewlineLF<char, Vec256Char>, Vec256Char> _t256 = new(_dialectChar);

    private readonly SimdTokenizer<char, NewlineLF<char, Vec512Char>, Vec512Char> _t512 = new(_dialectChar);

    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec128Byte>, Vec128Byte> _t128b = new(_dialectByte);

    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec256Byte>, Vec256Byte> _t256b = new(_dialectByte);

    private readonly SimdTokenizer<byte, NewlineLF<byte, Vec512Byte>, Vec512Byte> _t512b = new(_dialectByte);

    [Benchmark(Baseline = true)]
    public int V128()
    {
        if (!Vector128.IsHardwareAccelerated)
            throw new NotSupportedException();

        return Chars ? _t128.Tokenize(_metaBuffer, CharData, 0) : _t128b.Tokenize(_metaBuffer, ByteData, 0);
    }

    [Benchmark]
    public int V256()
    {
        if (!Vector256.IsHardwareAccelerated)
            throw new NotSupportedException();

        return Chars ? _t256.Tokenize(_metaBuffer, CharData, 0) : _t256b.Tokenize(_metaBuffer, ByteData, 0);
    }

    [Benchmark]
    public int V512()
    {
        if (!Vector512.IsHardwareAccelerated)
            throw new NotSupportedException();

        return Chars ? _t512.Tokenize(_metaBuffer, CharData, 0) : _t512b.Tokenize(_metaBuffer, ByteData, 0);
    }
}
