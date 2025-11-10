using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
[MemoryDiagnoser]
public class GetConverterBench
{
    private readonly Type[] types = typeof(int).Assembly.GetTypes();

    [Benchmark]
    public void GetConverters()
    {
        foreach (var type in types)
        {
            _ = DefaultConverters.GetText(type);
        }
    }
}
