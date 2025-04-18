using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Text;
using FlameCsv.Enumeration;
using FlameCsv.IO;
using Sylvan.Data;
using Sylvan.Data.Csv;

// ReSharper disable all

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
[SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
public partial class ReadObjects
{
    [Params(100, 5000, 20_000)] public int Records { get; set; } = 5000;
    [Params(false, true)] public bool Async { get; set; }

    private static readonly CsvOptions<char> _flameCsvOptions = new()
    {
        HasHeader = true, Newline = "\n", Converters = { new FloatTextParser(), }
    };

    private static readonly CsvHelper.Configuration.CsvConfiguration _helperConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = "\n", HasHeaderRecord = true, Delimiter = ",", Quote = '"',
    };

    private static readonly CsvDataReaderOptions _sylvanOptions = new()
    {
        CsvStyle = CsvStyle.Standard, Delimiter = ',', Quote = '"', HeaderComparer = StringComparer.OrdinalIgnoreCase,
    };

    [Benchmark]
    public async Task _FlameCsv()
    {
        if (Async)
        {
            await using var pipe = CsvPipeReader.Create(GetReader());
            await foreach (var entry in new CsvValueEnumerable<char, Entry>(
                               pipe,
                               _flameCsvOptions))
            {
                _ = entry;
            }
        }
        else
        {
            using var pipe = CsvPipeReader.Create(GetReader());
            foreach (var entry in new CsvValueEnumerable<char, Entry>(
                         pipe,
                         _flameCsvOptions))
            {
                _ = entry;
            }
        }
    }


    [Benchmark(Baseline = true)]
    public async Task _Flame_SrcGen()
    {
        if (Async)
        {
            await using var pipe = CsvPipeReader.Create(GetReader());
            await foreach (var entry in new CsvTypeMapEnumerable<char, Entry>(
                               pipe,
                               _flameCsvOptions,
                               EntryTypeMap.Default))
            {
                _ = entry;
            }
        }
        else
        {
            using var pipe = CsvPipeReader.Create(GetReader());
            foreach (var entry in new CsvTypeMapEnumerable<char, Entry>(pipe, _flameCsvOptions, EntryTypeMap.Default))
            {
                _ = entry;
            }
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        using var reader = GetReader();
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

        if (Async)
        {
            await foreach (var entry in csv.GetRecordsAsync<Entry>())
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in csv.GetRecords<Entry>())
            {
                _ = entry;
            }
        }
    }

    [Benchmark]
    public async Task _CsvHelper()
    {
        using var reader = GetReader();
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        if (Async)
        {
            await foreach (var entry in csv.GetRecordsAsync<Entry>())
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in csv.GetRecords<Entry>())
            {
                _ = entry;
            }
        }
    }

    private Stream GetStream()
        => Records switch
        {
            100 => _mmf0.CreateViewStream(0, _size0, MemoryMappedFileAccess.Read),
            5000 => _mmf1.CreateViewStream(0, _size1, MemoryMappedFileAccess.Read),
            20_000 => _mmf2.CreateViewStream(0, _size2, MemoryMappedFileAccess.Read),
            _ => throw new ArgumentOutOfRangeException(nameof(Records), Records, null)
        };

    private TextReader GetReader() => new StreamReader(GetStream(), Encoding.UTF8);

    private readonly MemoryMappedFile _mmf0;
    private readonly MemoryMappedFile _mmf1;
    private readonly MemoryMappedFile _mmf2;
    private readonly int _size0;
    private readonly int _size1;
    private readonly int _size2;

    public ReadObjects()
    {
        string path0 = Path.GetFullPath("Comparisons/Data/SampleCSVFile_100records.csv");
        string path1 = Path.GetFullPath("Comparisons/Data/SampleCSVFile_556kb.csv");
        string path2 = Path.GetFullPath("Comparisons/Data/SampleCSVFile_556kb_4x.csv");

        foreach (var p in (string[]) [path0, path1, path2])
        {
            using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.CopyTo(Stream.Null); // touch files to load into O/S cache
        }

        _size0 = (int)new FileInfo(path0).Length;
        _size1 = (int)new FileInfo(path1).Length;
        _size2 = (int)new FileInfo(path2).Length;

        _mmf0 = MemoryMappedFile.CreateFromFile(
            path: path0,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            access: MemoryMappedFileAccess.Read);

        _mmf1 = MemoryMappedFile.CreateFromFile(
            path: path1,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            access: MemoryMappedFileAccess.Read);

        _mmf2 = MemoryMappedFile.CreateFromFile(
            path: path2,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            access: MemoryMappedFileAccess.Read);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _mmf0?.Dispose();
        _mmf1?.Dispose();
        _mmf2?.Dispose();
    }
}
