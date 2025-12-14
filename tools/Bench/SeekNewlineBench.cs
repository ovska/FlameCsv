using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class SeekNewlineBench
{
    // [Params(5, 10, 15)]
    public int Interval { get; set; } = 10;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = rb.GetUnreadBuffer(0, out _);

        for (int i = 0; i < buffer.Fields.Length; i++)
        {
            var v = i % Interval == 0 ? ~0u : 0u;
            buffer.Fields[i] = v;
        }
    }

    private readonly RecordBuffer rb = new();

    [Benchmark(Baseline = true)]
    public void Exec()
    {
        rb._fieldCount = 0;
        rb._eolCount = 0;
        rb._eolIndex = 0;
        rb.SetFieldsRead(RecordBuffer.DefaultFieldBufferSize - 64);
    }
}
