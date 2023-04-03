using System.Globalization;
using System.Text;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[SimpleJob]
[MemoryDiagnoser]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvReadBench2
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");
    private static Stream GetFileStream() => new MemoryStream(_bytes);

    [Benchmark(Baseline = true)]
    public async Task Helper()
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var reader = new StreamReader(GetFileStream(), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<Entry>())
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameText()
    {
        using var reader = new StreamReader(GetFileStream());
        var options = new CsvTextReaderOptions();

        await foreach (var record in CsvReader.ReadAsync<Entry>(reader, options))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameUtf8()
    {
        var options = new CsvTextReaderOptions();

        await foreach (var record in CsvReader.ReadAsync<Entry>(GetFileStream(), options))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameText_P()
    {
        using var reader = new StreamReader(GetFileStream(), Encoding.UTF8);
        var options = new CsvTextReaderOptions();

        await foreach (var record in CsvReader.ReadAsync<Entry2_ASCII>(reader, options))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameUtf8_P()
    {
        var options = new CsvTextReaderOptions();

        await foreach (var record in CsvReader.ReadAsync<Entry2_UTF>(GetFileStream(), options))
        {
            _ = record;
        }
    }

    public sealed class Entry
    {
        [CsvHelper.Configuration.Attributes.Index(0), CsvIndex(0)]
        public int Index { get; set; }
        [CsvHelper.Configuration.Attributes.Index(1), CsvIndex(1)]
        public string? Name { get; set; }
        [CsvHelper.Configuration.Attributes.Index(2), CsvIndex(2)]
        public string? Contact { get; set; }
        [CsvHelper.Configuration.Attributes.Index(3), CsvIndex(3)]
        public int Count { get; set; }
        [CsvHelper.Configuration.Attributes.Index(4), CsvIndex(4)]
        public double Latitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(5), CsvIndex(5)]
        public double Longitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(6), CsvIndex(6)]
        public double Height { get; set; }
        [CsvHelper.Configuration.Attributes.Index(7), CsvIndex(7)]
        public string? Location { get; set; }
        [CsvHelper.Configuration.Attributes.Index(8), CsvIndex(8)]
        public string? Category { get; set; }
        [CsvHelper.Configuration.Attributes.Index(9), CsvIndex(9)]
        public double? Popularity { get; set; }
    }

    public sealed class Entry2_ASCII
    {
        [CsvHelper.Configuration.Attributes.Index(0), CsvIndex(0)]
        public int Index { get; set; }
        [CsvHelper.Configuration.Attributes.Index(1), CsvIndex(1)]
        public string? Name { get; set; }
        [CsvHelper.Configuration.Attributes.Index(2), CsvIndex(2)]
        public string? Contact { get; set; }
        [CsvHelper.Configuration.Attributes.Index(3), CsvIndex(3)]
        public int Count { get; set; }
        [CsvHelper.Configuration.Attributes.Index(4), CsvIndex(4)]
        public double Latitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(5), CsvIndex(5)]
        public double Longitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(6), CsvIndex(6)]
        public double Height { get; set; }
        [CsvHelper.Configuration.Attributes.Index(7), CsvIndex(7)]
        [CsvParserOverride(typeof(PoolingStringTextParser))]
        public string? Location { get; set; }
        [CsvHelper.Configuration.Attributes.Index(8), CsvIndex(8)]
        [CsvParserOverride(typeof(PoolingStringTextParser))]
        public string? Category { get; set; }
        [CsvHelper.Configuration.Attributes.Index(9), CsvIndex(9)]
        public double? Popularity { get; set; }
    }

    public sealed class Entry2_UTF
    {
        [CsvHelper.Configuration.Attributes.Index(0), CsvIndex(0)]
        public int Index { get; set; }
        [CsvHelper.Configuration.Attributes.Index(1), CsvIndex(1)]
        public string? Name { get; set; }
        [CsvHelper.Configuration.Attributes.Index(2), CsvIndex(2)]
        public string? Contact { get; set; }
        [CsvHelper.Configuration.Attributes.Index(3), CsvIndex(3)]
        public int Count { get; set; }
        [CsvHelper.Configuration.Attributes.Index(4), CsvIndex(4)]
        public double Latitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(5), CsvIndex(5)]
        public double Longitude { get; set; }
        [CsvHelper.Configuration.Attributes.Index(6), CsvIndex(6)]
        public double Height { get; set; }
        [CsvHelper.Configuration.Attributes.Index(7), CsvIndex(7)]
        [CsvParserOverride(typeof(PoolingStringUtf8Parser))]
        public string? Location { get; set; }
        [CsvHelper.Configuration.Attributes.Index(8), CsvIndex(8)]
        [CsvParserOverride(typeof(PoolingStringUtf8Parser))]
        public string? Category { get; set; }
        [CsvHelper.Configuration.Attributes.Index(9), CsvIndex(9)]
        public double? Popularity { get; set; }
    }
}
