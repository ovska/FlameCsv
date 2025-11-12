using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class PopNewlineBench
{
    private readonly RecordBuffer rb = new();

    [GlobalSetup]
    public void Setup()
    {
        for (ushort i = 0; i < rb._eols.Length; i++)
        {
            rb._eols[i] = (ushort)(i + 2);
        }
    }

    [Benchmark(Baseline = true)]
    public void Exec()
    {
        rb._eolIndex = 0;
        rb._eolCount = RecordBuffer.DefaultFieldBufferSize - 64;

        while (rb.TryPop(out _)) { }
    }
}
