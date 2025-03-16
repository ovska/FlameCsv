using System.Globalization;
using System.Text;
using FlameCsv.Enumeration;
using FlameCsv.IO;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using Sylvan.Data;
using Sylvan.Data.Csv;

// ReSharper disable all

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public partial class ReadObjects
{
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb.csv");

    private static readonly CsvOptions<char> _flameCsvOptions = new()
    {
        HasHeader = true, Newline = "\n", Converters = { new FloatTextParser(), }
    };

    private static readonly CsvHelper.Configuration.CsvConfiguration _helperConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = "\n",
        HasHeaderRecord = true,
        Delimiter = ",",
        Quote = '"',
    };

    private static readonly CsvDataReaderOptions _sylvanOptions = new()
    {
        CsvStyle = CsvStyle.Standard,
        Delimiter = ',',
        Quote = '"',
        HeaderComparer = StringComparer.OrdinalIgnoreCase,
    };

    private static FuncSpanT<double> _fastFloatFunc = (ReadOnlySpan<char> span) =>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            csFastFloat.FastDoubleParser.TryParseDouble(span, out double result),
            true);
        return result;
    };


    [Benchmark(Baseline = true)]
    public void _Flame_SrcGen()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var pipe = CsvPipeReader.Create(reader);
        foreach (var entry in new CsvTypeMapEnumerable<char, Entry>(pipe, _flameCsvOptions, EntryTypeMap.Default))
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _FlameCsv()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var pipe = CsvPipeReader.Create(reader);
        foreach (var entry in new CsvValueEnumerable<char, Entry>(pipe, _flameCsvOptions))
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _Sylvan()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

        foreach (var entry in csv.GetRecords<Entry>())
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _RecordParser()
    {
        using var streamReader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);

        var reader = new VariableLengthReaderBuilder<Entry>()
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
        var records = streamReader.ReadRecords(reader, readOptions);

        foreach (var entry in records)
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _CsvHelper()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        foreach (var entry in csv.GetRecords<Entry>())
        {
            _ = entry;
        }
    }

}
