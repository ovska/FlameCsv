using System.Text;
using FlameCsv.Attributes;

// ReSharper disable all

namespace FlameCsv.Benchmark;

[HideColumns("Error", "StdDev")]
[MemoryDiagnoser]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public partial class CsvReadBench
{
    static CsvReadBench()
    {
        _bytes = File.ReadAllBytes(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

        _newline = _bytes[_bytes.AsSpan().IndexOf((byte)'\n') - 1] == (byte)'\r' ? "\r\n" : "\n";

        var header = "Index,Name,Contact,Count,Latitude,Longitude,Height,Location,Category,Popularity"u8
            .ToArray()
            .Concat(Encoding.UTF8.GetBytes(_newline))
            .ToArray();

        _bytesHeader = new byte[header.Length + _bytes.Length];
        header.AsSpan().CopyTo(_bytesHeader);
        _bytes.CopyTo(_bytesHeader, header.Length);

        _chars = Encoding.UTF8.GetString(_bytes);
        _charsHeader = Encoding.UTF8.GetString(_bytesHeader);
    }

    private static string _newline;

    private static readonly byte[] _bytes;
    private static readonly byte[] _bytesHeader;

    private static readonly string _chars;
    private static readonly string _charsHeader;

    private MemoryStream GetFileStream() => new(Header ? _bytesHeader : _bytes);

    /*[Params(true, false)]*/
    public bool Header { get; set; } = true;

    private static readonly CsvOptions<char> _withHeader
        = new() { HasHeader = true, Newline = _newline, Converters = { new FloatTextParser() } };

    private static readonly CsvOptions<char> _withoutHeader
        = new() { HasHeader = false, Newline = _newline, Converters = { new FloatTextParser() } };

    private static readonly CsvOptions<byte> _bwithHeader
        = new() { HasHeader = true, Newline = _newline, Converters = { new FloatUtf8Parser() } };

    private static readonly CsvOptions<byte> _bwithoutHeader
        = new() { HasHeader = false, Newline = _newline, Converters = { new FloatUtf8Parser() } };

    private CsvOptions<char> OptionsInstance => Header ? _withHeader : _withoutHeader;
    private CsvOptions<byte> OptionsInstanceB => Header ? _bwithHeader : _bwithoutHeader;

    // [Benchmark]
    public void HelperUtf8()
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = _newline, HasHeaderRecord = Header,
        };

        using var reader = new StreamReader(GetFileStream(), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, config);

        foreach (var record in csv.GetRecords<Entry>())
        {
            _ = record;
        }
    }

    // [Benchmark(Baseline = true)]
    public void HelperText()
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine, HasHeaderRecord = Header,
        };

        using var reader = new StringReader(Header ? _charsHeader : _chars);
        using var csv = new CsvHelper.CsvReader(reader, config);

        foreach (var record in csv.GetRecords<Entry>())
        {
            _ = record;
        }
    }

    // [Benchmark]
    public void FlameText()
    {
        foreach (var record in CsvReader.Read<Entry>(Header ? _charsHeader : _chars, OptionsInstance))
        {
            _ = record;
        }
    }

    // [Benchmark]
    public void FlameUtf8()
    {
        foreach (var record in CsvReader.Read<Entry>(Header ? _bytesHeader : _bytes, OptionsInstanceB))
        {
            _ = record;
        }
    }

    // [Benchmark]
    public void FlameText_SG()
    {
        foreach (var record in CsvReader.Read(
                     Header ? _charsHeader : _chars,
                     EntryTypeMap_Text.Default,
                     OptionsInstance))
        {
            _ = record;
        }
    }

    [Benchmark]
    public void FlameUtf8_SG()
    {
        foreach (var record in CsvReader.Read(
                     Header ? _bytesHeader : _bytes,
                     EntryTypeMap_Utf8.Default,
                     OptionsInstanceB))
        {
            _ = record;
        }
    }

    [Benchmark]
    public void FlameParallel()
    {
        var query = CsvParallelReader.Read(
            Header ? _bytesHeader : _bytes,
            EntryTypeMap_Utf8.Default,
            OptionsInstanceB);

        foreach (var record in query.AsOrdered().WithMergeOptions(ParallelMergeOptions.NotBuffered))
        {
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

    [CsvTypeMap<char, CsvReadBench.Entry>]
    internal partial class EntryTypeMap_Text;

    [CsvTypeMap<byte, CsvReadBench.Entry>]
    internal partial class EntryTypeMap_Utf8;

    private sealed class FloatTextParser : CsvConverter<char, float>
    {
        public override bool TryParse(ReadOnlySpan<char> source, out float value)
        {
            return csFastFloat.FastFloatParser.TryParseFloat(source, out value);
        }

        public override bool TryFormat(Span<char> destination, float value, out int charsWritten)
        {
            return value.TryFormat(destination, out charsWritten);
        }
    }

    private sealed class FloatUtf8Parser : CsvConverter<byte, float>
    {
        public override bool TryParse(ReadOnlySpan<byte> source, out float value)
        {
            return csFastFloat.FastFloatParser.TryParseFloat(source, out value);
        }

        public override bool TryFormat(Span<byte> destination, float value, out int charsWritten)
        {
            return value.TryFormat(destination, out charsWritten);
        }
    }
}
