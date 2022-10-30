using System.Globalization;
using System.Text;
using CsvHelper;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Providers;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

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

        var options = new CsvHelper.TypeConversion.TypeConverterOptions { Formats = new[] { "yyyy'.'MM" } };
        csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(options);

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

        var config = CsvConfiguration.GetTextDefaultsBuilder(
                new CsvTextParserConfiguration{ DateTimeFormat = "yyyy'.'MM" })
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
            .AddParser(new YYYYMMParser())
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
        public DateTime Period { get; set; } // date

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
    
    internal sealed class YYYYMMParser : ParserBase<byte, DateTime>
    {
        public override bool TryParse(ReadOnlySpan<byte> span, out DateTime value)
        {
            if (span.Length == 7 && span[4] == '.')
            {
                var y1 = (uint)span[0] - '0';
                var y2 = (uint)span[1] - '0';
                var y3 = (uint)span[2] - '0';
                var y4 = (uint)span[3] - '0';
                var m1 = (uint)span[5] - '0';
                var m2 = (uint)span[6] - '0';

                if (y1 <= 9 && y2 <= 9 && y3 <= 9 && y4 <= 9 && m1 <= 9 && m2 <= 9)
                {
                    value = new(
                        (int)(y1 * 1000 + y2 * 100 + y3 * 10 + y4),
                        (int)(m1 * 10 + m2),
                        1,
                        0,
                        0,
                        0);

                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
