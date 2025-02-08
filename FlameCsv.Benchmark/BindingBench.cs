// ReSharper disable all
using System.Globalization;
using System.Runtime.InteropServices;
using FlameCsv.Attributes;
using FlameCsv.Binding;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public class BindingBench
{
    private const string CSV = "id,ticks,is_enabled,age,height\r\n1,1234567,true,40,182.57\r\n";

    private readonly CsvHelper.Configuration.CsvConfiguration _helperCfg = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.Replace("_", "").ToLowerInvariant(),
    };

    [Benchmark(Baseline = true)]
    public void Helper()
    {
        using var reader = new StringReader(CSV);
        using var csv = new CsvHelper.CsvReader(reader, _helperCfg);

        foreach (var _ in csv.GetRecords<Obj>())
        {
        }
    }

    [Benchmark]
    public void FlameReflect()
    {
        foreach (var _ in CsvReader.Read<Obj>(CSV, CsvOptions<char>.Default))
        {
        }
    }

    [Benchmark]
    public void FlameTypeMap()
    {
        foreach (var _ in CsvReader.Read(CSV, TestTypeMap.Instance, CsvOptions<char>.Default))
        {
        }
    }

    internal struct Obj
    {
        public int Id { get; set; }
        public long Ticks { get; set; }
        [CsvHeader("is_enabled")] public bool IsEnabled { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
    }
}

[CsvTypeMap<char, BindingBench.Obj>]
internal partial class TestTypeMap;
