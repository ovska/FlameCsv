using System.Globalization;
using System.Text;
using FlameCsv.Enumeration;
using Sylvan.Data;

namespace FlameCsv.Benchmark.Comparisons;

/*
config = config.AddJob(Job.Default.WithStrategy(RunStrategy.ColdStart)
    .WithIterationCount(1)
    .WithLaunchCount(1)
    .WithWarmupCount(1)
    .WithEnvironmentVariable("FLAMECSV_DISABLE_CACHING", "1"));
 */

[DryJob]
[SimpleJob]
[MemoryDiagnoser]
public class ReadCold
{
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_100records.csv");

    [Benchmark]
    public void _CsvHelper()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);

        foreach (var entry in csv.GetRecords<Entry>())
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _Sylvan()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader);

        foreach (var entry in csv.GetRecords<Entry>())
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _FlameCsv()
    {
        foreach (
            var entry in new CsvValueEnumerable<byte, Entry>(
                _data,
                _flameOptions ??= new CsvOptions<byte> { Converters = { new FloatUtf8Parser() } }
            )
        )
        {
            _ = entry;
        }
    }

    [Benchmark(Baseline = true)]
    public void _Flame_SrcGen()
    {
        foreach (
            var entry in new CsvTypeMapEnumerable<byte, Entry>(
                _data,
                _flameSgOptions ??= new CsvOptions<byte> { Converters = { new FloatUtf8Parser() } },
                EntryTypeMapUtf8.Default
            )
        )
        {
            _ = entry;
        }
    }

    private static CsvOptions<byte>? _flameSgOptions;
    private static CsvOptions<byte>? _flameOptions;
}
