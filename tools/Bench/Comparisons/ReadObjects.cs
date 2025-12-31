using System.Globalization;
using System.Text;
using Sylvan.Data;
using Sylvan.Data.Csv;
#if BENCHMARK_INCLUDE_UNCONVENTIONAL
using nietras.SeparatedValues;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using RecordParser.Parsers;
#endif

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public partial class ReadObjects
{
    // [Params(true, false)]
    public bool Async { get; set; }

    private MemoryStream GetStream() => new(_data, 0, _data.Length, writable: false, publiclyVisible: false);

    private StreamReader GetReader() => new(GetStream(), Encoding.UTF8);

    private readonly byte[] _data;

    public ReadObjects()
    {
        _data = Encoding.UTF8.GetBytes(
            File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv").ReplaceLineEndings("\n")
        );
    }

    private static readonly CsvOptions<char> _flameCsvOptions = new() { HasHeader = true, Newline = CsvNewline.LF };

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
        var builder = Csv.From(GetStream(), Encoding.UTF8);

        if (Async)
        {
            await foreach (var entry in builder.ReadAsync<Entry>(_flameCsvOptions))
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in builder.Read<Entry>(_flameCsvOptions))
            {
                _ = entry;
            }
        }
    }

    [Benchmark]
    public async Task _Flame_SrcGen()
    {
        var builder = Csv.From(GetStream(), Encoding.UTF8);

        if (Async)
        {
            await foreach (var entry in builder.ReadAsync(EntryTypeMap.Default, _flameCsvOptions))
            {
                _ = entry;
            }
        }
        else
        {
            foreach (var entry in builder.Read(EntryTypeMap.Default, _flameCsvOptions))
            {
                _ = entry;
            }
        }
    }

    [Benchmark]
    public async Task _FlameCsv_Reflection_Parallel()
    {
        var builder = Csv.From(GetStream(), Encoding.UTF8).AsParallel();

        if (Async)
        {
            await builder.ForEachUnorderedAsync<Entry>((_, _) => ValueTask.CompletedTask);
        }
        else
        {
            builder.ForEachUnordered<Entry>(_ => { });
        }
    }

    [Benchmark]
    public async Task _FlameCsv_SrcGen_Parallel()
    {
        var builder = Csv.From(GetStream(), Encoding.UTF8).AsParallel();

        if (Async)
        {
            await builder.ForEachUnorderedAsync(EntryTypeMap.Default, (_, _) => ValueTask.CompletedTask);
        }
        else
        {
            builder.ForEachUnordered(EntryTypeMap.Default, _ => { });
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        using var reader = GetReader();
        using var csv = CsvDataReader.Create(reader, _sylvanOptions);

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

#if BENCHMARK_INCLUDE_UNCONVENTIONAL
    [Benchmark]
    public void _RecordParser_Hardcoded()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        using var reader = GetReader();
        var rpReader = BuildRecordParserReader();

        foreach (var entry in reader.ReadRecords(rpReader, new VariableLengthReaderOptions { HasHeader = true }))
        {
            _ = entry;
        }
    }

    [Benchmark]
    public void _RecordParser_Parallel_Hardcoded()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        using var reader = GetReader();
        var rpReader = BuildRecordParserReader();

        foreach (
            var entry in reader.ReadRecords(
                rpReader,
                new VariableLengthReaderOptions
                {
                    HasHeader = true,
                    ParallelismOptions = new()
                    {
                        Enabled = true,
                        MaxDegreeOfParallelism = 4,
                        EnsureOriginalOrdering = false,
                    },
                }
            )
        )
        {
            _ = entry;
        }
    }

    [Benchmark]
    public async Task _Sep()
    {
        using var reader = CreateSepReader();

        if (Async)
        {
            await foreach (var r in reader)
            {
                _ = ParseSepDynamic(r);
            }
        }
        else
        {
            foreach (var r in reader)
            {
                _ = ParseSepDynamic(r);
            }
        }
    }

    [Benchmark]
    public async Task _Sep_Parallel()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        using var reader = CreateSepReader();
        foreach (var _ in reader.ParallelEnumerate(ParseSepDynamic)) { }
    }

    [Benchmark]
    public async Task _Sep_Hardcoded()
    {
        using var reader = CreateSepReader();

        if (Async)
        {
            await foreach (var r in reader)
            {
                _ = ParseSepHardcoded(r);
            }
        }
        else
        {
            foreach (var r in reader)
            {
                _ = ParseSepHardcoded(r);
            }
        }
    }

    [Benchmark]
    public async Task _Sep_Parallel_Hardcoded()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        using var reader = CreateSepReader();
        foreach (var _ in reader.ParallelEnumerate(ParseSepHardcoded)) { }
    }

    private SepReader CreateSepReader() =>
        Sep.Reader(o =>
                o with
                {
                    Sep = new Sep(','),
                    CultureInfo = CultureInfo.InvariantCulture,
                    HasHeader = true,
                    Unescape = true,
                    ColNameComparer = StringComparer.OrdinalIgnoreCase,
                    DisableFastFloat = true, // keep libs in even footing re: parsing
                }
            )
            .From(GetStream());

    private static Entry ParseSepHardcoded(SepReader.Row r)
    {
        return new Entry()
        {
            Index = r[0].Parse<int>(),
            Name = r[1].ToString(),
            Contact = r[2].ToString(),
            Count = r[3].Parse<int>(),
            Latitude = r[4].Parse<double>(),
            Longitude = r[5].Parse<double>(),
            Height = r[6].Parse<double>(),
            Location = r[7].ToString(),
            Category = r[8].ToString(),
            Popularity = r[9].Span.IsEmpty ? null : r[9].Parse<double>(),
        };
    }

    private static Entry ParseSepDynamic(SepReader.Row r)
    {
        return new Entry()
        {
            Index = r["Index"].Parse<int>(),
            Name = r["Name"].ToString(),
            Contact = r["Contact"].ToString(),
            Count = r["Count"].Parse<int>(),
            Latitude = r["Latitude"].Parse<double>(),
            Longitude = r["Longitude"].Parse<double>(),
            Height = r["Height"].Parse<double>(),
            Location = r["Location"].ToString(),
            Category = r["Category"].ToString(),
            Popularity = r["Popularity"].Span.IsEmpty ? null : r[9].Parse<double>(),
        };
    }

    private static IVariableLengthReader<Entry> BuildRecordParserReader()
    {
        return new VariableLengthReaderBuilder<Entry>()
            .Map(x => x.Index, 0)
            .Map(x => x.Name, 1)
            .Map(x => x.Contact, 2)
            .Map(x => x.Count, 3)
            .Map(x => x.Latitude, 4)
            .Map(x => x.Longitude, 5)
            .Map(x => x.Height, 6)
            .Map(x => x.Location, 7)
            .Map(x => x.Category, 8)
            .Map(x => x.Popularity, 9)
            .Build(",", CultureInfo.InvariantCulture);
    }
#endif
}
