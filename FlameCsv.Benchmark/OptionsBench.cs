using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Gen0")]
public class OptionsBench
{
    [Benchmark(Baseline = true)]
    public void FromOptions()
    {
        _ = new CsvOptions<char>().GetConverter<DayOfWeek>();
    }

    [Benchmark]
    public void FromDefault()
    {
        _ = new CsvOptions<char>().GetOrCreate(static o => new EnumTextConverter<DayOfWeek>(o));
    }
}
