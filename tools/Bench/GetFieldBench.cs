// ReSharper disable all

using System.Runtime.Intrinsics;
using System.Text;
using FlameCsv.Intrinsics;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class GetFieldBench
{
    private readonly byte[] _data;
    private readonly Meta[] _metaBuffer = new Meta[24 * 65535];
    private readonly int _count;
    private readonly CsvReader<byte> _reader = new(CsvOptions<byte>.Default, EmptyBufferReader<byte>.Instance);

    public GetFieldBench()
    {
        _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
        var tokenizer = new SimdTokenizer<byte, NewlineLF, Vec256>(CsvOptions<byte>.Default);
        _count = tokenizer.Tokenize(_metaBuffer.AsSpan(1), _data, 0) + 1;
    }

    [Benchmark(Baseline = true)]
    public void GetField()
    {
        var record = new CsvRecordRef<byte>(_reader, ref _data[0], _metaBuffer.AsSpan(0, _count));

        for (int i = 0; i < record.FieldCount; i++)
        {
            _ = record[i];
        }
    }
}
