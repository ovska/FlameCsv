using System;
using System.Buffers;
using System.Text;
using CsvHelper;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using nietras.SeparatedValues;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Gen0")]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvEnumerateBench
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");
    private static readonly string _chars = Encoding.ASCII.GetString(_bytes);
    private static MemoryStream GetFileStream() => new MemoryStream(_bytes);
    private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());
    private static readonly ReadOnlySequence<char> _charSeq = new(_chars.AsMemory());

    //[Benchmark(Baseline = true)]
    //public void CsvHelper_Sync()
    //{
    //    using var reader = new StringReader(_chars);

    //    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
    //    {
    //        NewLine = Environment.NewLine,
    //        HasHeaderRecord = false,
    //    };

    //    using var csv = new CsvHelper.CsvReader(reader, config);

    //    while (csv.Read())
    //    {
    //        for (int i = 0; i < 10; i++)
    //        {
    //            _ = csv.GetField(i);
    //        }
    //    }
    //}

    //[Benchmark]
    //public void Flame_Utf8()
    //{
    //    foreach (var record in new CsvRecordEnumerable<byte>(in _byteSeq, CsvUtf8Options.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    ////[Benchmark]
    ////public async ValueTask Flame_Utf8_Async()
    ////{
    ////    using var stream = GetFileStream();

    ////    await foreach (var record in CsvReader.EnumerateAsync(stream, CsvUtf8Options.Default))
    ////    {
    ////        foreach (var field in record)
    ////        {
    ////            _ = field;
    ////        }
    ////    }
    ////}

    //[Benchmark]
    //public void Flame_Char()
    //{
    //    foreach (var record in new CsvRecordEnumerable<char>(in _charSeq, CsvTextOptions.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    //[Benchmark]
    //public async ValueTask Flame_Char_Async()
    //{
    //    await using var stream = GetFileStream();
    //    using var reader = new StreamReader(stream, Encoding.ASCII, false);

    //    await foreach (var record in CsvReader.EnumerateAsync(reader, CsvTextOptions.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    [Benchmark]
    public void FlameUTF2()
    {
        byte[]? unescapeArray = null;
        Span<byte> unescapeBuffer = stackalloc byte[128];
        using var parser = CsvParser<byte>.Create(CsvUtf8Options.Default);
        parser.Reset(new ReadOnlySequence<byte>(_bytes));

        while (parser.TryReadLine(out var line, out var meta))
        {
            CsvFieldReader<byte> state = new(
                CsvUtf8Options.Default,
                line,
                unescapeBuffer,
                ref unescapeArray,
                meta.quoteCount,
                meta.escapeCount);

            while (!state.End)
            {
                _ = RFC4180Mode<byte>.ReadNextField(ref state);
            }
        }

        ArrayPool<byte>.Shared.EnsureReturned(ref unescapeArray);
    }

    [Benchmark]
    public void Sepp()
    {
        var reader = nietras.SeparatedValues.Sep.Reader(o => o with
        {
            Sep = new nietras.SeparatedValues.Sep(','),
            CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
            HasHeader = false,
        }).From(_bytes);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i];
            }
        }
    }
}
