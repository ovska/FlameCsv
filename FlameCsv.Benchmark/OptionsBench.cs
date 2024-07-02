using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Gen0")]
public class OptionsBench
{
    private readonly CsvTextOptions _options = new();

    [Benchmark(Baseline = true)]
    public void FromOptions()
    {
        _ = _options.GetConverter<string>();
    }

    [Benchmark]
    public void FromDefault()
    {
        _ = DefaultConverters.CreateString(_options);
    }
}
