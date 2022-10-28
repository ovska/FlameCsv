using System.Globalization;
using System.Text;
using CsvHelper;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Providers;

namespace FlameCsv.Benchmark;

// [SimpleJob(BenchmarkDotNet.Engines.RunStrategy.ColdStart, launchCount: 10)]
[SimpleJob]
[MemoryDiagnoser]
public class CsvReadBench
{
    private static readonly byte[] _file = File.ReadAllBytes("/home/sipi/test.csv");

    [Benchmark(Baseline = true)]
    public async ValueTask CsvHelper()
    {
        await using var stream = new MemoryStream(_file);
        using var reader = new StreamReader(stream, Encoding.ASCII, false);
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<Item>())
        {
            _ = record;
        }
    }

    [Benchmark]
    public async ValueTask FlameCsv_ASCII()
    {
        await using var stream = new MemoryStream(_file);
        using var reader = new StreamReader(stream, Encoding.ASCII, false);

        var config = CsvConfiguration<char>.DefaultBuilder
            .SetParserOptions(CsvParserOptions<char>.Environment)
            .SetBinder(new IndexBindingProvider<char>())
            .Build();

        await foreach (var item in Readers.CsvReader.ReadAsync<Item>(config, reader))
        {
            _ = item;
        }
    }

    [Benchmark]
    public async ValueTask FlameCsv_Utf8()
    {
        await using var stream = new MemoryStream(_file);

        var config = CsvConfiguration<byte>.DefaultBuilder
            .SetParserOptions(CsvParserOptions<byte>.Environment)
            .SetBinder(new IndexBindingProvider<byte>())
            .Build();

        await foreach (var item in Readers.CsvReader.ReadAsync<Item>(config, stream))
        {
            _ = item;
        }
    }

    public enum StatusEnum
    {
        X = 0,
        F = 1,
    }

    public enum UnitsEnum
    {
        Unknown = 0,
        Number = 1,
    }

    // Series_reference,Period,Data_value,Suppressed,STATUS,UNITS,Magnitude,Subject,Group,Series_title_1,Series_title_2,Series_title_3,Series_title_4,Series_title_5
    public class Item
    {
        [CsvHelper.Configuration.Attributes.Index(0), IndexBinding(0)]
        public string? SeriesReference { get; set; }

        [CsvHelper.Configuration.Attributes.Index(1), IndexBinding(1)]
        public string? Period { get; set; } // date

        [CsvHelper.Configuration.Attributes.Index(2), IndexBinding(2)]
        public int DataValue { get; set; }

        [CsvHelper.Configuration.Attributes.Index(3), IndexBinding(3)]
        public string? Suppressed { get; set; }

        [CsvHelper.Configuration.Attributes.Index(4), IndexBinding(4)]
        public StatusEnum Status { get; set; }

        [CsvHelper.Configuration.Attributes.Index(5), IndexBinding(5)]
        public UnitsEnum Units { get; set; }

        [CsvHelper.Configuration.Attributes.Index(6), IndexBinding(6)]
        public int Magnitude { get; set; }

        [CsvHelper.Configuration.Attributes.Index(7), IndexBinding(7)]
        public string? Subject { get; set; }

        [CsvHelper.Configuration.Attributes.Index(8), IndexBinding(8)]
        public string? Group { get; set; }

        [CsvHelper.Configuration.Attributes.Index(9), IndexBinding(9)]
        public string? SeriesTitle1 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(10), IndexBinding(10)]
        public string? SeriesTitle2 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(11), IndexBinding(11)]
        public string? SeriesTitle3 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(12), IndexBinding(12)]
        public string? SeriesTitle4 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(13), IndexBinding(13)]
        public string? SeriesTitle5 { get; set; }
    }
}
