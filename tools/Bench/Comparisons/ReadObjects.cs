using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
[SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
public partial class ReadObjects
{
    public int Records { get; set; } = 20000;

    [Params(false)]
    public bool Async { get; set; }

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

    // [Benchmark]
    public async Task _FlameCsv()
    {
        if (Async)
        {
            await foreach (var entry in Csv.From(GetStream()).WithUtf8Encoding().ReadAsync<Entry>(_flameCsvOptions))
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in Csv.From(GetStream()).WithUtf8Encoding().Read<Entry>(_flameCsvOptions))
            {
                _ = entry;
            }
        }
    }

    // [Benchmark]
    public async Task _Parallel()
    {
        if (Async)
        {
            await Csv.From(_string2)
                .AsParallel()
                .ForEachUnorderedAsync(EntryTypeMap.Default, (_, _) => ValueTask.CompletedTask);
        }
        else
        {
            Csv.From(_string2).AsParallel().ForEachUnordered(EntryTypeMap.Default, _ => { });
        }
    }

    // [Benchmark]
    public async Task AsyncEnumerable1()
    {
        await foreach (var _ in Csv.From(_string2).AsParallel().ReadUnorderedAsync<Entry>(EntryTypeMap.Default)) { }
    }

    [Benchmark(Baseline = true)]
    public async Task _Flame_SrcGen()
    {
        if (Async)
        {
            await foreach (
                var entry in Csv.From(GetStream())
                    .WithUtf8Encoding()
                    .ReadAsync<Entry>(EntryTypeMap.Default, _flameCsvOptions)
            )
            {
                _ = entry;
            }
        }
        else
        {
            foreach (
                var entry in Csv.From(GetStream())
                    .WithUtf8Encoding()
                    .Read<Entry>(EntryTypeMap.Default, _flameCsvOptions)
            )
            {
                _ = entry;
            }
        }
    }

    // [Benchmark]
    // public async Task _Sylvan()
    // {
    //     using var reader = GetReader();
    //     using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

    //     if (Async)
    //     {
    //         await foreach (var entry in csv.GetRecordsAsync<Entry>())
    //         {
    //             _ = entry;
    //         }
    //     }
    //     else
    //     {
    //         foreach (var entry in csv.GetRecords<Entry>())
    //         {
    //             _ = entry;
    //         }
    //     }
    // }

    // [Benchmark]
    // public async Task _CsvHelper()
    // {
    //     using var reader = GetReader();
    //     using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

    //     if (Async)
    //     {
    //         await foreach (var entry in csv.GetRecordsAsync<Entry>())
    //         {
    //             _ = entry;
    //         }
    //     }
    //     else
    //     {
    //         foreach (var entry in csv.GetRecords<Entry>())
    //         {
    //             _ = entry;
    //         }
    //     }
    // }

    private Stream GetStream() =>
        Records switch
        {
            100 => new MemoryStream(_data0, 0, _data0.Length, writable: false, publiclyVisible: false),
            5000 => new MemoryStream(_data1, 0, _data1.Length, writable: false, publiclyVisible: false),
            20_000 => new MemoryStream(_data2, 0, _data2.Length, writable: false, publiclyVisible: false),
            _ => throw new ArgumentOutOfRangeException(nameof(Records), Records, null),
        };

    private TextReader GetReader() => new StreamReader(GetStream(), Encoding.UTF8);

    private readonly byte[] _data0;
    private readonly byte[] _data1;
    private readonly byte[] _data2;
    private readonly string _string2;

    public ReadObjects()
    {
        _data0 = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_100records.csv");
        _data1 = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb.csv");
        _data2 = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb_4x.csv");
        _string2 = Encoding.UTF8.GetString(_data2);
    }
}
