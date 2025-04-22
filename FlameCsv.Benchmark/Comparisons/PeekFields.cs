using System.Buffers;
using System.Globalization;
using System.Text;
using FlameCsv.Reading;
using nietras.SeparatedValues;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using Sylvan.Data.Csv;

// ReSharper disable all

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public partial class PeekFields
{
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");

    private static readonly CsvOptions<byte> _flameCsvOptions = new() { HasHeader = true, Newline = "\n" };

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

    private static readonly FuncSpanT<double> _fastFloatFunc = (ReadOnlySpan<char> span) =>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            csFastFloat.FastDoubleParser.TryParseDouble(span, out double result),
            true);
        return result;
    };

    [Benchmark(Baseline = true)]
    public void _FlameCsv()
    {
        ReadOnlySequence<byte> sequence = new(_data);

        double sum = 0;

        using var enumerator = new CsvReader<byte>(_flameCsvOptions, in sequence).ParseRecords().GetEnumerator();

        // skip first record
        _ = enumerator.MoveNext();

        while (enumerator.MoveNext())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                csFastFloat.FastDoubleParser.TryParseDouble(enumerator.Current[11], out double result),
                true);

            sum += result;
        }

        _ = sum;
    }

    [Benchmark]
    public void _Sep()
    {
        using var reader = Sep.Reader(
        o => o with
        {
            Sep = new Sep(','),
            CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
            HasHeader = true,
        })
        .From(_data);

        double sum = 0;

        foreach (var row in reader)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                csFastFloat.FastDoubleParser.TryParseDouble(row[11].Span, out double result),
                true);

            sum += result;
        }

        _ = sum;
    }

    [Benchmark]
    public void _Sylvan()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

        double sum = 0;

        while (csv.Read())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                csFastFloat.FastDoubleParser.TryParseDouble(csv.GetFieldSpan(11), out double result),
                true);

            sum += result;
        }

        _ = sum;
    }

    [Benchmark]
    public void _RecordParser()
    {
        using var streamReader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        var reader = new VariableLengthReaderSequentialBuilder<RecordEntry>()
            .Skip(10)
            .Map(r => r.Value, _fastFloatFunc)
            .Build(",", CultureInfo.InvariantCulture);

        var readOptions = new VariableLengthReaderOptions { HasHeader = true, ContainsQuotedFields = true };
        var records = streamReader.ReadRecords(reader, readOptions);

        double sum = 0;

        foreach (var entry in records)
        {
            sum += entry.Value;
        }

        _ = sum;
    }

    record struct RecordEntry(double Value);

    [Benchmark]
    public void _CsvHelper()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        double sum = 0;

        // skip first record
        _ = csv.Read();

        while (csv.Read())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                csFastFloat.FastDoubleParser.TryParseDouble(csv.GetField(11), out double result),
                true);

            sum += result;
        }

        _ = sum;
    }
}
