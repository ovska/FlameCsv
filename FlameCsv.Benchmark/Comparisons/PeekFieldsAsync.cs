using System.Buffers;
using System.Globalization;
using System.Text;
using csFastFloat;
using CsvHelper.Configuration;
using FlameCsv.Reading;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class PeekFieldsAsync
{
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");

    private static readonly CsvOptions<byte> _flameCsvOptions = new() { HasHeader = true, Newline = "\n" };

    private static readonly CsvConfiguration _helperConfig = new(CultureInfo.InvariantCulture)
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

    [Benchmark(Baseline = true)]
    public async Task _FlameCsv()
    {
        ReadOnlySequence<byte> sequence = new(_data);

        double sum = 0;

        await using var enumerator
            = new CsvReader<byte>(_flameCsvOptions, in sequence).ParseRecordsAsync().GetAsyncEnumerator();

        // skip first record
        _ = await enumerator.MoveNextAsync();

        while (await enumerator.MoveNextAsync())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                FastDoubleParser.TryParseDouble(enumerator.Current[11], out double result),
                true);

            sum += result;
        }

        _ = sum;
    }

    [Benchmark]
    public async Task _Sep()
    {
        using var reader = await Sep
            .Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = CultureInfo.InvariantCulture,
                    HasHeader = true,
                })
            .FromAsync(_data);

        double sum = 0;

        await foreach (var row in reader)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                FastDoubleParser.TryParseDouble(row[11].Span, out double result),
                true);

            sum += result;
        }

        _ = sum;
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        await using var csv = await CsvDataReader.CreateAsync(reader, _sylvanOptions);

        double sum = 0;

        while (await csv.ReadAsync())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                FastDoubleParser.TryParseDouble(csv.GetFieldSpan(11), out double result),
                true);

            sum += result;
        }

        _ = sum;
    }


    [Benchmark]
    public async Task _CsvHelper()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        double sum = 0;

        // skip first record
        _ = await csv.ReadAsync();

        while (await csv.ReadAsync())
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                FastDoubleParser.TryParseDouble(csv.GetField(11), out double result),
                true);

            sum += result;
        }

        _ = sum;
    }
}
