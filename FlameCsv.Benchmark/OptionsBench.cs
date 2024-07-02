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
    [Benchmark(Baseline = true)]
    public void FromOptions()
    {
        _ = new CsvTextOptions().GetConverter<DayOfWeek>();
    }

    [Benchmark]
    public void FromDefault()
    {
        _ = DefaultConverters.Create<DayOfWeek>(new CsvTextOptions());
    }
}
