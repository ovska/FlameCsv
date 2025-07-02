// ReSharper disable all

using FlameCsv.Intrinsics;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class FindNewlineBench
{
    private readonly byte[] _dataUnquoted;
    private readonly byte[] _dataQuoted;
    private readonly Meta[] _metaUnquoted = new Meta[24 * 65535];
    private readonly Meta[] _metaQuoted = new Meta[24 * 65535];
    private readonly int _countQuoted;
    private readonly int _countUnquoted;
    private readonly CsvReader<byte> _reader = new(CsvOptions<byte>.Default, EmptyBufferReader<byte>.Instance);

    // [Params(true, false)]
    public bool Quoted { get; set; }

    public bool Raw { get; set; }

    public FindNewlineBench()
    {
        _dataUnquoted = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
        _dataQuoted = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb_4x.csv");

        var tokenizer = new SimdTokenizer<byte, NewlineLF, Vec256>(CsvOptions<byte>.Default);

        _countQuoted = tokenizer.Tokenize(_metaUnquoted.AsSpan(1), _dataUnquoted, 0) + 1;
        _countUnquoted = tokenizer.Tokenize(_metaQuoted.AsSpan(1), _dataQuoted, 0) + 1;

        _buffer = new();
    }

    private readonly MetaBuffer _buffer;

    [Benchmark(Baseline = true)]
    public void TryPop()
    {
        // ugly hacks
        _buffer.UnsafeGetArrayRef() = [];
        _buffer.Dispose(); // reset counts
        _buffer.UnsafeGetArrayRef() = Quoted ? _metaQuoted : _metaUnquoted;
        _buffer.SetFieldsRead(Quoted ? _countQuoted : _countUnquoted);

        while (_buffer.TryPop(out _)) { }
    }
}
