using System.Text;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
//[SimpleJob]
[MemoryDiagnoser]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvReadBench
{
    static CsvReadBench()
    {
        _bytes = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

        var header = "Index,Name,Contact,Count,Latitude,Longitude,Height,Location,Category,Popularity\r\n"u8;
        _bytes_h = new byte[header.Length + _bytes.Length];
        header.CopyTo(_bytes_h);
        _bytes.CopyTo(_bytes_h, header.Length);
    }

    private static readonly byte[] _bytes;
    private static readonly byte[] _bytes_h;

    private MemoryStream GetFileStream() => new MemoryStream(Header ? _bytes_h : _bytes);

    /*[Params(true, false)]*/
    public bool Header { get; set; } = true;

    private static readonly CsvTextOptions _withHeader = new CsvTextOptions { HasHeader = true };
    private static readonly CsvTextOptions _withoutHeader = new CsvTextOptions { HasHeader = false };

    private static readonly CsvUtf8Options _bwithHeader = new CsvUtf8Options { HasHeader = true };
    private static readonly CsvUtf8Options _bwithoutHeader = new CsvUtf8Options { HasHeader = false };

    private CsvOptions<char> OptionsInstance => Header ? _withHeader : _withoutHeader;
    private CsvOptions<byte> OptionsInstanceB => Header ? _bwithHeader : _bwithoutHeader;

    [Benchmark(Baseline = true)]
    public async Task Helper()
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = Header,
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
        await foreach (var record in CsvReader.ReadAsync<Entry>(
            GetFileStream(),
            Encoding.UTF8,
            OptionsInstance))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameUtf8()
    {
        await foreach (var record in CsvReader.ReadAsync<Entry>(
            GetFileStream(),
            OptionsInstanceB))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameText_SG()
    {
        await foreach (var record in CsvReader.ReadAsync<Entry>(
            GetFileStream(),
            EntryTypeMap_Text.Instance,
            OptionsInstance,
            encoding: Encoding.UTF8))
        {
            _ = record;
        }
    }

    [Benchmark]
    public async Task FlameUtf8_SG()
    {
        await foreach (var record in CsvReader.ReadAsync<Entry>(
            GetFileStream(),
            EntryTypeMap_Utf8.Instance,
            OptionsInstanceB))
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
}

[CsvTypeMap<char, CsvReadBench.Entry>]
internal partial class EntryTypeMap_Text;

[CsvTypeMap<byte, CsvReadBench.Entry>]
internal partial class EntryTypeMap_Utf8;
