#if false // TODO: tokenizer_refactor
using FlameCsv.Intrinsics;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class AllFieldsBench
{
    private readonly byte[] _dataUnquoted = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
    private readonly byte[] _dataQuoted = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb_4x.csv");

    private readonly SimdTokenizer<byte, NewlineLF, Vec256> _simdTokenizer = new(CsvOptions<byte>.Default);
    private readonly SimdTokenizer<byte, NewlineLF> _altTokenizer = new(CsvOptions<byte>.Default);

    private readonly Meta[] _hugeMetaBuffer = new Meta[24 * 65535];
    private readonly int[] _eolBuffer = new int[24 * 65535];

    [Params(false, true)]
    public bool Quoted { get; set; }

    private byte[] Data => Quoted ? _dataQuoted : _dataUnquoted;

    [Benchmark(Baseline = true)]
    public void Old()
    {
        using var reader = new CsvReader<byte>(CsvOptions<byte>.Default, Data);
        var mb = new MetaBuffer();
        mb.UnsafeGetArrayRef() = _hugeMetaBuffer;

        int count = _simdTokenizer.Tokenize(mb.GetUnreadBuffer(out int startIndex), Data, startIndex);
        mb.SetFieldsRead(count);

        while (mb.TryPop(out _)) { }
    }

    [Benchmark]
    public void New()
    {
        using var reader = new CsvReader<byte>(CsvOptions<byte>.Default, Data);
        var mb = new RecordBuffer();
        mb.UnsafeGetArrayRef() = _hugeMetaBuffer;
        mb.UnsafeGetEOLArrayRef() = _eolBuffer;

        _altTokenizer.Tokenize(mb, Data);

        while (mb.TryPop(out _)) { }
    }
}
#endif
