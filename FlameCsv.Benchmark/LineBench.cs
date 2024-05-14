using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
[HideColumns("Error", "StdDev", "Gen0")]
public class LineBench
{
    private static readonly char[] _chars = File.ReadAllText("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv").ToCharArray();
    private static readonly byte[] _bytes = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

    private readonly CsvParser<char> _pc = CsvParser<char>.Create(CsvTextOptions.Default);
    private readonly CsvParser<byte> _pb = CsvParser<byte>.Create(CsvUtf8Options.Default);

    [Benchmark(Baseline = true)]
    public void Char_Parser()
    {
        var parser = _pc;
        parser.Reset(new(_chars));

        while (!parser.End)
            parser.TryReadLine(out _, out _, isFinalBlock: false);
    }

    //[Benchmark]
    //public void Byte_parser()
    //{
    //    var parser = _pb;
    //    parser.Reset(new(_bytes));

    //    while (!parser.End)
    //        parser.TryReadLine(out _, out _);
    //}

    [Benchmark]
    public void Char_Slicer()
    {
        Span<CsvParser.Slice> ranges = stackalloc CsvParser.Slice[128];
        var reader = new LineBufferer<char>(CsvTextOptions.Default);
        ReadOnlySpan<char> remaining = _chars;
        BufferResult result;

        do
        {
            result = reader.ReadLines(remaining, ranges);
            foreach (var slice in ranges[..result.LinesRead])
                _ = remaining.Slice(slice.Index, slice.Length);
            remaining = remaining.Slice(result.Consumed);
        }
        while (result.LinesRead > 0);
    }

    //[Benchmark]
    //public void Byte_Slicer()
    //{
    //    Span<Slice> ranges = stackalloc Slice[128];
    //    var reader = new LineBufferer<byte>(in _byteContext.Dialect);
    //    ReadOnlySpan<byte> remaining = _bytes;
    //    BufferResult result;

    //    do
    //    {
    //        result = reader.ReadLines(remaining, ranges);
    //        foreach (var slice in ranges[..result.LinesRead])
    //            _ = remaining.Slice(slice.Index, slice.Length);
    //        remaining = remaining.Slice(result.Consumed);
    //    }
    //    while (result.LinesRead > 0);
    //}

    //[Benchmark]
    //public void Byte_Slicer_vec()
    //{
    //    Span<Slice> ranges = stackalloc Slice[128];
    //    var reader = new LineBufferer<byte>(in _byteContext.Dialect);
    //    ReadOnlySpan<byte> remaining = _bytes;
    //    BufferResult result;

    //    do
    //    {
    //        result = reader.ReadLines(remaining, ranges, vectorized: true);
    //        foreach (var slice in ranges[..result.LinesRead])
    //            _ = remaining.Slice(slice.Index, slice.Length);
    //        remaining = remaining.Slice(result.Consumed);
    //    }
    //    while (result.LinesRead > 0);
    //}
}
