// ReSharper disable all

using System.Buffers;
using System.Text;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class SeekNewlineBench
{
    [Params(true, false)] public bool AltData { get; set; }
    [Params(true, false)] public bool CRLF { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string path = AltData ? @"Comparisons/Data/65K_Records_Data.csv" : @"Comparisons/Data/SampleCSVFile_556kb.csv";

        _string = File
            .ReadAllText(path, Encoding.UTF8)
            .ReplaceLineEndings(CRLF ? "\r\n" : "\n");

        _byteArray = Encoding.UTF8.GetBytes(_string);
        _byteSeq = new ReadOnlySequence<byte>(_byteArray);
        _charSeq = new ReadOnlySequence<char>(_string.AsMemory());

        _optionsByteLF = new() { Newline = "\n" };
        _optionsCharLF = new() { Newline = "\n" };
        _optionsByteCRLF = new() { Newline = "\r\n" };
        _optionsCharCRLF = new() { Newline = "\r\n" };
    }

    private byte[] _byteArray = [];
    private string _string = "";
    private ReadOnlySequence<byte> _byteSeq;
    private ReadOnlySequence<char> _charSeq;

    private CsvOptions<byte> _optionsByteLF = null!;
    private CsvOptions<char> _optionsCharLF = null!;
    private CsvOptions<byte> _optionsByteCRLF = null!;
    private CsvOptions<char> _optionsCharCRLF = null!;

    private CsvOptions<byte> OptionsByte => CRLF ? _optionsByteCRLF : _optionsByteLF;
    private CsvOptions<char> OptionsChar => CRLF ? _optionsCharCRLF : _optionsCharLF;

    [Benchmark(Baseline = true)]
    public void Utf8()
    {
        foreach (var _ in new CsvReader<byte>(OptionsByte, in _byteSeq).ParseRecords())
        {
        }
    }

    [Benchmark]
    public void Utf16()
    {
        foreach (var _ in new CsvReader<char>(OptionsChar, in _charSeq).ParseRecords())
        {
        }
    }
}
