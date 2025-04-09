using System.Globalization;
using System.Text;
using FlameCsv.Enumeration;
using FlameCsv.IO;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class ReadObjectsAsync
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

    [Benchmark(Baseline = true)]
    public async Task _FlameCsv()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        await using var pipe = CsvPipeReader.Create(reader);
        await foreach (var entry in new CsvValueEnumerable<char, Entry>(pipe, _flameCsvOptions).ConfigureAwait(false))
        {
            _ = entry;
        }
    }

    [Benchmark]
    public async Task _Flame_SrcGen()
    {
        using var reader = new StreamReader(new MemoryStream(_data), Encoding.UTF8);
        await using var pipe = CsvPipeReader.Create(reader);
        await foreach (var entry in new CsvTypeMapEnumerable<char, Entry>(pipe, _flameCsvOptions, EntryTypeMap.Default)
                           .ConfigureAwait(false))
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
