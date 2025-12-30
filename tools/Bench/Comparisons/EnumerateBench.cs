using System.Buffers;
using System.Globalization;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using nietras.SeparatedValues;
using RecordParser.Extensions;

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class EnumerateBench
{
    [Params(true, false)]
    public bool Quoted { get; set; }

    [Params(true, false)]
    public bool Async { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _data = Encoding.UTF8.GetBytes(
            File.ReadAllText(Quoted ? "Comparisons/Data/customers-100000.csv" : "Comparisons/Data/65K_Records_Data.csv")
                .ReplaceLineEndings("\n")
        );

        _flameCsvOptions = new() { Newline = CsvNewline.LF, Quote = Quoted ? '"' : null };

        _helperConfig = new(CultureInfo.InvariantCulture)
        {
            NewLine = "\n",
            Delimiter = ",",
            Mode = CsvHelper.CsvMode.NoEscape,
        };

        _sylvanOptions = new()
        {
            CsvStyle = Sylvan.Data.Csv.CsvStyle.Standard,
            Delimiter = ',',
            Quote = '"',
            HeaderComparer = StringComparer.OrdinalIgnoreCase,
        };

        _configureSep = o =>
            o with
            {
                Sep = new Sep(','),
                CultureInfo = CultureInfo.InvariantCulture,
                HasHeader = true,
                Unescape = Quoted,
                DisableQuotesParsing = !Quoted,
            };

        _rpOptions = new VariableLengthReaderRawOptions
        {
            HasHeader = true,
            ContainsQuotedFields = Quoted,
            ColumnCount = Quoted ? 12 : 14,
            Separator = ",",
        };

        _rpParallelOptions = new VariableLengthReaderRawOptions
        {
            HasHeader = true,
            ContainsQuotedFields = Quoted,
            ColumnCount = Quoted ? 12 : 14,
            Separator = ",",
            ParallelismOptions = new()
            {
                Enabled = true,
                EnsureOriginalOrdering = true,
                MaxDegreeOfParallelism = 4,
            },
        };
    }

    private byte[] _data = null!;
    private CsvOptions<byte> _flameCsvOptions = null!;
    private CsvHelper.Configuration.CsvConfiguration _helperConfig = null!;
    private Sylvan.Data.Csv.CsvDataReaderOptions _sylvanOptions = null!;
    private Func<SepReaderOptions, SepReaderOptions> _configureSep = null!;
    private VariableLengthReaderRawOptions _rpOptions = null!;
    private VariableLengthReaderRawOptions _rpParallelOptions = null!;

    private byte[] GetData() => _data;

    private MemoryStream GetStream() => new(_data, 0, _data.Length, writable: false, publiclyVisible: false);

    private StreamReader GetReader() => new(GetStream(), Encoding.UTF8);

    [Benchmark(Baseline = true)]
    public async Task _FlameCsv()
    {
        CsvReader<byte> reader = new(_flameCsvOptions, GetData());

        if (Async)
        {
            await foreach (var r in reader.ParseRecordsAsync())
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    _ = r[i];
                }
            }
        }
        else
        {
            foreach (var r in reader.ParseRecords())
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    _ = r[i];
                }
            }
        }
    }

    [Benchmark]
    public async Task _Sep()
    {
        using SepReader reader = Sep.Reader(_configureSep).From(GetData());

        if (Async)
        {
            await foreach (var row in reader)
            {
                for (int i = 0; i < row.ColCount; i++)
                {
                    _ = row[i];
                }
            }
        }
        else
        {
            foreach (var row in reader)
            {
                for (int i = 0; i < row.ColCount; i++)
                {
                    _ = row[i];
                }
            }
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(4 * 4096); // help sylvan a bit, it allocates this otherwise
        using var reader = GetReader();
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, buffer, _sylvanOptions);

        if (Async)
        {
            while (await csv.ReadAsync(CancellationToken.None))
            {
                for (int i = 0; i < csv.FieldCount; i++)
                {
                    _ = csv.GetFieldSpan(i);
                }
            }
        }
        else
        {
            while (csv.Read())
            {
                for (int i = 0; i < csv.FieldCount; i++)
                {
                    _ = csv.GetFieldSpan(i);
                }
            }
        }

        ArrayPool<char>.Shared.Return(buffer);
    }

    [Benchmark]
    public async Task _CsvHelper()
    {
        using var reader = GetReader();
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        if (Async)
        {
            while (await csv.ReadAsync())
            {
                for (int i = 0; i < csv.ColumnCount; i++)
                {
                    _ = csv.GetField(i);
                }
            }
        }
        else
        {
            while (csv.Read())
            {
                for (int i = 0; i < csv.ColumnCount; i++)
                {
                    _ = csv.GetField(i);
                }
            }
        }
    }

    [Benchmark]
    public void _RecordParser()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        int fieldCount = Quoted ? 12 : 14;

        var r = GetReader()
            .ReadRecordsRaw(
                _rpOptions,
                getField =>
                {
                    for (int i = 0; i < fieldCount; i++)
                    {
                        _ = getField(i);
                    }

                    return default(object?);
                }
            );

        foreach (var _ in r) { }
    }

    [Benchmark]
    public void _RecordParser_Parallel()
    {
        if (Async)
        {
            throw new NotSupportedException();
        }

        int fieldCount = Quoted ? 12 : 14;

        var r = GetReader()
            .ReadRecordsRaw(
                _rpParallelOptions,
                getField =>
                {
                    for (int i = 0; i < fieldCount; i++)
                    {
                        _ = getField(i);
                    }

                    return default(object?);
                }
            );
        foreach (var _ in r) { }
    }
}
