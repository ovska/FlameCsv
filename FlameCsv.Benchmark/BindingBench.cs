using System.Globalization;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public class BindingBench
{
    private const string CSV = "id,name,is_enabled,age,height\r\n1,Bob,true,40,182.57";

    [Benchmark(Baseline = true)]
    public void Helper()
    {
        var cfg = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Replace("_", "").ToLowerInvariant(),
        };

        using var reader = new StringReader(CSV);
        using var csv = new CsvHelper.CsvReader(reader, cfg);
        _ = csv.GetRecords<Obj>().ToList();
    }

    [Benchmark]
    public void FlameReflect()
    {
        _ = CsvReader.Read<Obj>(CSV, CsvTextReaderOptions.Default).ToList();
    }

    [Benchmark]
    public void FlameTypeMap()
    {
        _ = CsvReader.Read(CSV, typeMap: TestTypeMap.Instance, CsvTextReaderOptions.Default).ToList();

    }

    internal class Obj
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        [CsvHeader("is_enabled")] public bool IsEnabled { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
    }
}

[CsvTypeMap<char, BindingBench.Obj>]
internal partial class TestTypeMap
{
}
