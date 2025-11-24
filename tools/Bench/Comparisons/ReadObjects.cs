using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
[SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
public partial class ReadObjects
{
    public int Records { get; set; } = 20000;

    [Params(true)]
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
            await using var reader = CsvBufferReader.Create(GetStream(), encoding: Encoding.UTF8);
            await foreach (var entry in CsvReader.Read<Entry>(GetStream(), _flameCsvOptions))
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in CsvReader.Read<Entry>(GetStream(), _flameCsvOptions))
            {
                _ = entry;
            }
        }
    }

    [Benchmark]
    public async Task _Parallel()
    {
        using var reader = new ParallelMemoryReader<char>(_string2.AsMemory(), _flameCsvOptions);

        if (Async)
        {
            await CsvParallel.ForEachAsync(EntryTypeMap.Default, reader, (list, ct) => default, _flameCsvOptions);
        }
        else
        {
            CsvParallel.ForEach(EntryTypeMap.Default, reader, list => { }, _flameCsvOptions);
        }
    }

    [Benchmark(Baseline = true)]
    public async Task _Flame_SrcGen()
    {
        if (Async)
        {
            await foreach (
                var entry in CsvReader.Read<Entry>(GetStream(), EntryTypeMap.Default, options: _flameCsvOptions)
            )
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in CsvReader.Read<Entry>(GetStream(), EntryTypeMap.Default, options: _flameCsvOptions))
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
