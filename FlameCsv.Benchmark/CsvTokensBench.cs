namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
public class CsvTokensBench
{
    private static readonly CsvTokens<char> _tokens = CsvTokens<char>.Environment;

    [Benchmark]
    public void FromReadonly()
    {
        _tokens.ThrowIfInvalid();
    }

    [Benchmark(Baseline = true)]
    public void FromStatic()
    {
        CsvTokens<char>.Environment.ThrowIfInvalid();
    }
}
