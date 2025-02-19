using System.Buffers;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Mean", "Error", "StdDev", "RatioSD")]
public class VectorizationBench
{
    private readonly Meta[] _metas = new Meta[1024 * 128];

    private readonly byte[] _data1 = File.ReadAllBytes(
        "C:/Users/Sipi/Downloads/geographic-units-by-industry-and-statistical-area-2000-2024-descending-order-february-2024.csv");

    private readonly byte[] _data2 = File.ReadAllBytes(
        "C:/Users/Sipi/Downloads/annual-enterprise-survey-2023-financial-year-provisional.csv");

    [Params(true, false)] public bool Short { get; set; }

    private byte[] GetData() => Short ? _data1 : _data2;

    [Benchmark(Baseline = false)]
    public void Sequential()
    {
        _ = SequentialParser<byte>.Core(
            in CsvOptions<byte>.Default.Dialect,
            NewlineBuffer<byte>.CRLF,
            GetData(),
            _metas);
    }

    [Benchmark(Baseline = true)]
    public void Vectorized()
    {
        _ = FieldParser<byte, NewlineParserTwo<byte, Vec256Byte>, Vec256Byte>.Core(
            (byte)',',
            (byte)'"',
            new((byte)'\r', (byte)'\n'),
            GetData(),
            _metas);
    }

    [Benchmark(Baseline = false)]
    public void NoBuffering()
    {
        using var parser = CsvParser.Create(_nobuffering, new ReadOnlySequence<byte>(GetData()));

        int i = 0;
        while (parser.TryReadLine(out var line, false) && ((i += (line.Fields.Length - 1)) < 131072))
        {
        }
    }

    private static readonly CsvOptions<byte> _nobuffering = new() { Newline = "\r\n", NoReadAhead = true };
}
