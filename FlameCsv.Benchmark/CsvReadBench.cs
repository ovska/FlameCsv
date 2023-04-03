using System.Globalization;
using System.Text;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Benchmark;

// [SimpleJob(BenchmarkDotNet.Engines.RunStrategy.ColdStart, launchCount: 10)]
[SimpleJob]
[MemoryDiagnoser]
public class CsvReadBench
{
    private static readonly byte[] _file = File.ReadAllBytes("/home/sipi/test.csv");
    private static readonly string _string = Encoding.UTF8.GetString(_file);

    [Benchmark(Baseline = true)]
    public void CsvHelper()
    {
        using var reader = new StringReader(_string);
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvHelper.CsvReader(reader, config);

        var options = new CsvHelper.TypeConversion.TypeConverterOptions { Formats = new[] { "yyyy'.'MM" } };
        csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(options);

        foreach (var record in csv.GetRecords<Item>())
        {
            _ = record;
        }
    }

    [Benchmark]
    public void FlameCsv_ASCII()
    {
        var config = new CsvTextReaderOptions
        {
            DateTimeFormat = "yyyy'.'MM",
            FormatProvider = CultureInfo.InvariantCulture,
            Newline = "\n".AsMemory(),
        };

        foreach (var item in CsvReader.Read<Item>(_string, config))
        {
            _ = item;
        }
    }

    [Benchmark]
    public void FlameCsv_Utf8()
    {
        var config = new CsvUtf8ReaderOptions
        {
            Newline = "\n"u8.ToArray(),
            Parsers = { new YYYYMMParser() },
        };

        foreach (var item in CsvReader.Read<Item>(_file, config))
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
        [CsvHelper.Configuration.Attributes.Index(0), CsvIndex(0)]
        public string? SeriesReference { get; set; }

        [CsvHelper.Configuration.Attributes.Index(1), CsvIndex(1)]
        public DateTime Period { get; set; } // date

        [CsvHelper.Configuration.Attributes.Index(2), CsvIndex(2)]
        public int DataValue { get; set; }

        [CsvHelper.Configuration.Attributes.Index(3), CsvIndex(3)]
        public string? Suppressed { get; set; }

        [CsvHelper.Configuration.Attributes.Index(4), CsvIndex(4)]
        public StatusEnum Status { get; set; }

        [CsvHelper.Configuration.Attributes.Index(5), CsvIndex(5)]
        public UnitsEnum Units { get; set; }

        [CsvHelper.Configuration.Attributes.Index(6), CsvIndex(6)]
        public int Magnitude { get; set; }

        [CsvHelper.Configuration.Attributes.Index(7), CsvIndex(7)]
        public string? Subject { get; set; }

        [CsvHelper.Configuration.Attributes.Index(8), CsvIndex(8)]
        public string? Group { get; set; }

        [CsvHelper.Configuration.Attributes.Index(9), CsvIndex(9)]
        public string? SeriesTitle1 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(10), CsvIndex(10)]
        public string? SeriesTitle2 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(11), CsvIndex(11)]
        public string? SeriesTitle3 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(12), CsvIndex(12)]
        public string? SeriesTitle4 { get; set; }

        [CsvHelper.Configuration.Attributes.Index(13), CsvIndex(13)]
        public string? SeriesTitle5 { get; set; }
    }

    internal sealed class YYYYMMParser : Parsers.ParserBase<byte, DateTime>
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
                    value = new DateTime(
                        year: (int)(y1 * 1000 + y2 * 100 + y3 * 10 + y4),
                        month: (int)(m1 * 10 + m2),
                        day: 1);

                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
