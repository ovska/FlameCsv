// ReSharper disable all
using System.Globalization;
using System.Runtime.InteropServices;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace FlameCsv.Benchmark;

[DryJob]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public partial class BindingBench
{
    private const string CSV = "id,ticks,isenabled,age,height\r\n1,1234567,true,40,182.57\r\n";

    private readonly CsvHelper.Configuration.CsvConfiguration _helperCfg = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.ToLowerInvariant(),
    };

    private static readonly CsvDataReaderOptions _sylvanOptions = new()
    {
        CsvStyle = CsvStyle.Standard, Delimiter = ',', Quote = '"', HeaderComparer = StringComparer.OrdinalIgnoreCase,
    };

    [Benchmark(Baseline = true)]
    public void _FlameCsv_SrcGen()
    {
        foreach (var _ in CsvReader.Read(CSV, TestTypeMap.Default, CsvOptions<char>.Default))
        {
        }
    }

    [Benchmark]
    public void _FlameCsv_Reflect()
    {
        foreach (var _ in CsvReader.Read<Obj>(CSV, CsvOptions<char>.Default))
        {
        }
    }

    [Benchmark]
    public void _Sylvan()
    {
        using var reader = new StringReader(CSV);
        using var csv = Sylvan.Data.Csv.CsvDataReader.Create(reader, _sylvanOptions);

        foreach (var _ in csv.GetRecords<Entry>())
        {
        }
    }

    [Benchmark]
    public void _CsvHelper()
    {
        using var reader = new StringReader(CSV);
        using var csv = new CsvHelper.CsvReader(reader, _helperCfg);

        foreach (var _ in csv.GetRecords<Obj>())
        {
        }
    }

    internal struct Obj
    {
        public int Id { get; set; }
        public long Ticks { get; set; }
        public bool IsEnabled { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
    }

    [CsvTypeMap<char, BindingBench.Obj>]
    private partial class TestTypeMap;
}

