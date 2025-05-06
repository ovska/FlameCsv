using System.Buffers;
using System.Text;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

#pragma warning disable CA1859 // Use concrete types when possible for improved performance

namespace FlameCsv.Benchmark;

public class Utf8StreamBench
{
    [Params(false, true)] public bool Randomize { get; set; }

    [Benchmark(Baseline = true)]
    public void _StreamReader()
    {
        using var reader = GetReader();

        int offsetIndex = 0;

        while (true)
        {
            var result = reader.Read();
            if (result.IsCompleted) break;
            int advanceBy = Randomize ? _offsets[offsetIndex++ % _offsets.Length] : 0;
            reader.Advance(result.Buffer.Length - advanceBy);
        }
    }

    [Benchmark]
    public void _Utf8StreamReader()
    {
        using var reader = GetStream();

        int offsetIndex = 0;

        while (true)
        {
            var result = reader.Read();
            if (result.IsCompleted) break;
            int advanceBy = Randomize ? _offsets[offsetIndex++ % _offsets.Length] : 0;
            reader.Advance(Math.Max(result.Buffer.Length - advanceBy, 0));
        }
    }

    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");

    private static ICsvBufferReader<char> GetReader()
        => CsvBufferReader.Create(new StreamReader(new MemoryStream(_data), Encoding.UTF8, bufferSize: 16 * 1024));

    private static ICsvBufferReader<char> GetStream()
        => new Utf8StreamReader(new MemoryStream(_data), MemoryPool<char>.Shared, new());

    private static readonly int[] _offsets = Enumerable.Range(0, 1024).Select(_ => Random.Shared.Next(0, 128)).ToArray();
}
