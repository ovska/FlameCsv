using System.Globalization;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;

#pragma warning disable CA1859
// ReSharper disable all

namespace FlameCsv.Benchmark.Comparisons;

[MemoryDiagnoser]
public class EnumerateBench
{
    [Params(false, true)] public bool ReadFields { get; set; }
    [Params(false, true)] public bool Async { get; set; }

    private static readonly CsvOptions<byte> _flameCsvOptions = new() { HasHeader = true, Newline = "\n" };

    private static readonly CsvHelper.Configuration.CsvConfiguration _helperConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = "\n",
        HasHeaderRecord = false,
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
        bool readFields = ReadFields;

        var parser = new CsvReader<byte>(
            _flameCsvOptions,
            CsvBufferReader.Create(GetStream(), options: new() { NoDirectBufferAccess = true }));

        if (Async)
        {
            await foreach (var r in parser.ParseRecordsAsync())
            {
                if (readFields)
                {
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        _ = r.GetRawSpan(i);
                    }
                }
            }
        }
        else
        {
            foreach (var r in parser.ParseRecords())
            {
                if (readFields)
                {
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        _ = r.GetRawSpan(i);
                    }
                }
            }
        }
    }

    [Benchmark]
    public async Task _Sep()
    {
        using var reader = Sep
            .Reader(o => o with
            {
                Sep = new Sep(','),
                CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                HasHeader = true,
                Unescape = false,
            })
            .From(GetReader());

        bool readFields = ReadFields;

        if (Async)
        {
            await foreach (var row in reader)
            {
                if (readFields)
                {
                    for (int i = 0; i < row.ColCount; i++)
                    {
                        _ = row[i];
                    }
                }
            }
        }
        else
        {
            foreach (var row in reader)
            {
                if (readFields)
                {
                    for (int i = 0; i < row.ColCount; i++)
                    {
                        _ = row[i];
                    }
                }
            }
        }
    }

    [Benchmark]
    public async Task _Sylvan()
    {
        using var reader = GetReader();
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

        bool readFields = ReadFields;

        if (Async)
        {
            while (await csv.ReadAsync())
            {
                if (readFields)
                {
                    for (int i = 0; i < csv.FieldCount; i++)
                    {
                        _ = csv.GetFieldSpan(i);
                    }
                }
            }
        }
        else
        {
            while (csv.Read())
            {
                if (readFields)
                {
                    for (int i = 0; i < csv.FieldCount; i++)
                    {
                        _ = csv.GetFieldSpan(i);
                    }
                }
            }
        }
    }

    [Benchmark]
    public async Task _CsvHelper()
    {
        using var reader = GetReader();
        using var csv = new CsvHelper.CsvReader(reader, _helperConfig);

        bool readFields = ReadFields;

        if (Async)
        {
            while (await csv.ReadAsync())
            {
                if (readFields)
                {
                    for (int i = 0; i < csv.ColumnCount; i++)
                    {
                        _ = csv.GetField(i);
                    }
                }
            }
        }
        else
        {
            while (csv.Read())
            {
                if (readFields)
                {
                    for (int i = 0; i < csv.ColumnCount; i++)
                    {
                        _ = csv.GetField(i);
                    }
                }
            }
        }
    }

    private Stream GetStream() => new MemoryStream(_data, 0, _data.Length, writable: false, publiclyVisible: false);
    private TextReader GetReader() => new StreamReader(GetStream(), Encoding.UTF8);

    private readonly byte[] _data;

    public EnumerateBench()
    {
        _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");
    }
}
