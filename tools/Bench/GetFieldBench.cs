#if false


using FlameCsv.Intrinsics;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class GetFieldBench
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

    public GetFieldBench()
    {
        _dataUnquoted = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
        _dataQuoted = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb_4x.csv");

        var tokenizer = new SimdTokenizer<byte, NewlineLF>(CsvOptions<byte>.Default);

        _countQuoted = tokenizer.Tokenize(_metaUnquoted.AsSpan(1), _dataUnquoted, 0) + 1;
        _countUnquoted = tokenizer.Tokenize(_metaQuoted.AsSpan(1), _dataQuoted, 0) + 1;
    }

    [Benchmark(Baseline = true)]
    public void GetField()
    {
        var record = new CsvRecordRef<byte>(
            _reader,
            ref (Quoted ? ref _dataQuoted[0] : ref _dataUnquoted[0]),
            Quoted ? _metaQuoted.AsSpan(0, _countUnquoted) : _metaUnquoted.AsSpan(0, _countQuoted)
        );

        if (Raw)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record.GetRawSpan(i);
            }
        }
        else
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                _ = record[i];
            }
        }
    }
}
#endif
