using System.Globalization;
using System.Text;
using FlameCsv.Enumeration;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using RecordParser.Parsers;
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
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_10records.csv");

    private static readonly FuncSpanT<double> _fastFloatFunc = span =>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            csFastFloat.FastDoubleParser.TryParseDouble(span, out double result),
            true);
        return result;
    };

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
    public void _RecordParser()
    {
        using var streamReader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);

        _recordParser ??= new VariableLengthReaderBuilder<Entry>()
            .Map(e => e.Index, indexColumn: 0)
            .Map(e => e.Name, indexColumn: 1)
            .Map(e => e.Contact, indexColumn: 2)
            .Map(e => e.Count, indexColumn: 3)
            .Map(e => e.Latitude, indexColumn: 4, _fastFloatFunc)
            .Map(e => e.Longitude, indexColumn: 5, _fastFloatFunc)
            .Map(e => e.Height, indexColumn: 6, _fastFloatFunc)
            .Map(e => e.Location, indexColumn: 7)
            .Map(e => e.Category, indexColumn: 8)
            .Map(e => e.Popularity, indexColumn: 9)
            .Build(",", CultureInfo.InvariantCulture);

        var readOptions = new VariableLengthReaderOptions { HasHeader = true, ContainsQuotedFields = true };
        var records = streamReader.ReadRecords(_recordParser, readOptions);

        foreach (var entry in records)
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
        foreach (var entry in new CsvValueEnumerable<byte, Entry>(
                     _data,
                     _flameOptions ??= new CsvOptions<byte> { Converters = { new FloatUtf8Parser() } }))
        {
            _ = entry;
        }
    }

    [Benchmark(Baseline = true)]
    public void _Flame_SrcGen()
    {
        foreach (var entry in new CsvTypeMapEnumerable<byte, Entry>(
                     _data,
                     _flameSgOptions ??= new CsvOptions<byte> { Converters = { new FloatUtf8Parser() } },
                     EntryTypeMapUtf8.Default))
        {
            _ = entry;
        }
    }

    private static CsvOptions<byte>? _flameSgOptions;
    private static CsvOptions<byte>? _flameOptions;

    private static IVariableLengthReader<Entry>? _recordParser;
}
