using System.Globalization;
using System.Text;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class ReadObjectsAsync
{
    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb.csv");

    private static readonly CsvOptions<char> _flameCsvOptions = new()
    {
        HasHeader = true,
        Newline = CsvNewline.LF,
        Converters = { new FloatTextParser(), new DoubleTextParser() },
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

    [Benchmark(Baseline = true)]
    public async Task _FlameCsv()
    {
        await foreach (
            var entry in Csv.From(new MemoryStream(_data)).WithUtf8Encoding().ReadAsync<Entry>().ConfigureAwait(false)
        )
        {
            _ = entry;
        }
    }

    [Benchmark]
    public async Task _Flame_SrcGen()
    {
        await foreach (
            var entry in Csv.From(new MemoryStream(_data))
                .WithUtf8Encoding()
                .ReadAsync(EntryTypeMap.Default, _flameCsvOptions)
                .ConfigureAwait(false)
        )
        {
            _ = entry;
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        await using var csv = await CsvDataReader.CreateAsync(reader, _sylvanOptions);

        await foreach (var entry in csv.GetRecordsAsync<Entry>().ConfigureAwait(false))
        {
            _ = entry;
        }
    }

    [Benchmark]
    public async Task _CsvHelper()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        await foreach (var entry in csv.GetRecordsAsync<Entry>().ConfigureAwait(false))
        {
            _ = entry;
        }
    }
}
