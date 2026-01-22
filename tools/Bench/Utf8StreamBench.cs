using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

#pragma warning disable CA1859 // Use concrete types when possible for improved performance

namespace FlameCsv.Benchmark;

public class Utf8StreamReadBench
{
    [Params(false, true)]
    public bool Randomize { get; set; }

    [Benchmark(Baseline = true)]
    public void _ReaderWrapper()
    {
        using var reader = GetReader();

        int offsetIndex = 0;

        while (true)
        {
            var result = reader.Read();
            if (result.IsCompleted)
                break;
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
            if (result.IsCompleted)
                break;
            int advanceBy = Randomize ? _offsets[offsetIndex++ % _offsets.Length] : 0;
            reader.Advance(Math.Max(result.Buffer.Length - advanceBy, 0));
        }
    }

    [Benchmark]
    public void _StreamReader()
    {
        using var reader = new StreamReader(
            new MemoryStream(_data),
            Encoding.UTF8,
            bufferSize: CsvIOOptions.DefaultBufferSize
        );

        char[] buffer = ArrayPool<char>.Shared.Rent(CsvIOOptions.DefaultBufferSize);
        int offsetIndex = 0;

        while (true)
        {
            int endOffset = Randomize ? _offsets[offsetIndex++ % _offsets.Length] : 0;
            int read = reader.Read(buffer.AsSpan(0, buffer.Length - endOffset));

            if (read == 0)
                break;
        }

        ArrayPool<char>.Shared.Return(buffer);
    }

    private static readonly byte[] _data = File.ReadAllBytes("Comparisons/Data/65K_Records_Data.csv");

    private static ICsvBufferReader<char> GetReader() =>
        CsvBufferReader.Create(
            new StreamReader(new MemoryStream(_data), Encoding.UTF8, bufferSize: CsvIOOptions.DefaultBufferSize)
        );

    private static ICsvBufferReader<char> GetStream() => new Utf8StreamReader(new MemoryStream(_data), new());

    private static readonly int[] _offsets = Enumerable
        .Range(0, 1024)
        .Select(_ => Random.Shared.Next(0, 128))
        .ToArray();
}

public class Utf8StreamWriteBench
{
    // [Params(false, true)]
    public bool Randomize { get; set; } = true;

    [Benchmark(Baseline = true)]
    public void _WriterWrapper()
    {
        var writer = GetWriter();
        int offsetIndex = 0;
        int written = 0;

        while (written < 1024 * 1024 * 16)
        {
            int count = Randomize ? 64 + _offsets[offsetIndex++ % _offsets.Length] : 96;

            Span<char> buffer = writer.GetSpan(count);
            buffer.Clear();
            writer.Advance(count);
            written += count;

            if (writer.NeedsDrain)
            {
                writer.Drain();
            }
        }

        writer.Complete(null);
    }

    [Benchmark]
    public void _Utf8StreamWriter()
    {
        var writer = GetStream();
        int offsetIndex = 0;
        int written = 0;

        while (written < 1024 * 1024 * 16)
        {
            int count = Randomize ? 64 + _offsets[offsetIndex++ % _offsets.Length] : 96;

            Span<char> buffer = writer.GetSpan(count);
            buffer.Clear();
            writer.Advance(count);
            written += count;

            if (writer.NeedsDrain)
            {
                writer.Drain();
            }
        }

        writer.Complete(null);
    }

    [Benchmark]
    public void _TextWriter()
    {
        using var writer = new StreamWriter(
            GetDestination(),
            Encoding.UTF8,
            bufferSize: CsvIOOptions.DefaultBufferSize
        );
        int offsetIndex = 0;
        int written = 0;

        Span<char> buffer = stackalloc char[256];

        while (written < 1024 * 1024 * 16)
        {
            int count = Randomize ? 64 + _offsets[offsetIndex++ % _offsets.Length] : 96;

            var current = buffer.Slice(0, count);
            current.Clear();
            writer.Write(current);
            written += count;
            writer.Flush();
        }
    }

    private static byte[] _data = (
        (Func<byte[]>)(
            () =>
            {
                byte[] data = new byte[1024 * 1024];
                Random.Shared.NextBytes(data);
                return data;
            }
        )
    )();

    private static ICsvBufferWriter<char> GetWriter() =>
        new TextBufferWriter(
            new StreamWriter(GetDestination(), Encoding.UTF8, bufferSize: CsvIOOptions.DefaultBufferSize),
            new()
        );

    private static ICsvBufferWriter<char> GetStream() => new Utf8StreamWriter(GetDestination(), new());

    private static readonly int[] _offsets = Enumerable.Range(0, 1024).Select(_ => Random.Shared.Next(0, 64)).ToArray();

    private static Stream GetDestination() => new ArrayPoolBufferWriter<byte>().AsStream();
}
