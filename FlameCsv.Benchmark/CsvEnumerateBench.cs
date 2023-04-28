using System.Buffers;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvEnumerateBench
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");
    private static readonly string _chars = Encoding.ASCII.GetString(_bytes);
    private static Stream GetFileStream() => new MemoryStream(_bytes);
    private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());
    private static readonly ReadOnlySequence<char> _charSeq = new(_chars.AsMemory());

    [Benchmark(Baseline = true)]
    public void CsvHelper_Sync()
    {
        using var reader = new StringReader(_chars);

        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvHelper.CsvReader(reader, config);

        while (csv.Read())
        {
            for (int i = 0; i < 10; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public async ValueTask CsvHelper_Async()
    {
        await using var stream = GetFileStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false);

        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvHelper.CsvReader(reader, config);

        while (await csv.ReadAsync())
        {
            for (int i = 0; i < 10; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public void Flame_Utf8()
    {
        foreach (var record in new CsvRecordEnumerable<byte>(in _byteSeq, CsvUtf8ReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }

    [Benchmark]
    public async ValueTask Flame_Utf8_Async()
    {
        using var stream = GetFileStream();

        await foreach (var record in CsvReader.EnumerateAsync(stream, CsvUtf8ReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }

    [Benchmark]
    public void Flame_Char()
    {
        foreach (var record in new CsvRecordEnumerable<char>(_charSeq, CsvTextReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }

    [Benchmark]
    public async ValueTask Flame_Char_Async()
    {
        await using var stream = GetFileStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false);

        await foreach (var record in CsvReader.EnumerateAsync(reader, CsvTextReaderOptions.Default))
        {
            foreach (var field in record)
            {
                _ = field;
            }
        }
    }
}
