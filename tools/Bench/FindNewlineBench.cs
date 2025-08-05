#if false
// ReSharper disable all

using FlameCsv.Intrinsics;
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
    private readonly int[] _indexes;
    private readonly int _indexCount;

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

        _indexes = new int[65535];
        _indexCount = 0;

        ReadOnlySpan<Meta> toIterate = (Quoted ? _metaQuoted : _metaUnquoted).AsSpan(
            1,
            Quoted ? _countQuoted : _countUnquoted
        );

        for (int i = 0; i < toIterate.Length; i++)
        {
            if (toIterate[i].IsEOL)
            {
                _indexes[_indexCount++] = i;
            }
        }
    }

    private readonly MetaBuffer _buffer;

    [Benchmark]
    public void FromIndices()
    {
        int indexCount = _indexCount;

        ReadOnlySpan<Meta> toIterate = (Quoted ? _metaQuoted : _metaUnquoted).AsSpan(
            1,
            Quoted ? _countQuoted : _countUnquoted
        );

        int previous = 0;

        for (int i = 0; i < indexCount; i++)
        {
            int index = _indexes[i];

            _ = new MetaBuffer.MetaSegment
            {
                array = _buffer.UnsafeGetArrayRef(),
                count = index - previous,
                offset = previous,
            };
            previous = index;
        }
    }

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
#endif
