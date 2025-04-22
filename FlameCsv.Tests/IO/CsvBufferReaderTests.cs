using System.Text;
using FlameCsv.IO;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.IO;

public class CsvBufferReaderTests
{
    [Fact]
    public static void Test2()
    {
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        using var textReader = new StringReader(_data);
        using var reader = new TextBufferReader(
            textReader,
            pool,
            new CsvReaderOptions { MinimumReadSize = 4, BufferSize = 64 });

        var result = reader.Read();
        Assert.Equal(_data[..64], result.Buffer.ToString());
        Assert.False(result.IsCompleted);

        // advance by 20
        reader.Advance(20);
        result = reader.Read();
        Assert.Equal(_data[20..84], result.Buffer.ToString());
        Assert.False(result.IsCompleted);

        reader.Advance(result.Buffer.Length);
        result = reader.Read();
        Assert.Equal(_data[84..], result.Buffer.ToString());
        Assert.False(result.IsCompleted);

        result = reader.Read();
        Assert.Equal(_data[84..], result.Buffer.ToString());
        Assert.True(result.IsCompleted);

        reader.Advance(result.Buffer.Length);

        result = reader.Read();
        Assert.Empty(result.Buffer.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public static void Test3()
    {
        ReadOnlySpan<byte> seq = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_"u8;
        List<byte> bytes = [];

        for (int i = 0; i < 10; i++)
        {
            bytes.AddRange(seq);
        }

        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        using var textReader = new StreamReader(new MemoryStream(bytes.ToArray()), Encoding.UTF8);
        using var reader = new TextBufferReader(
            textReader,
            pool,
            new CsvReaderOptions { MinimumReadSize = seq.Length / 2, BufferSize = seq.Length });

        for (int i = 0; i < 10; i++)
        {
            var result = reader.Read();
            Assert.Equal(Encoding.UTF8.GetString(seq), result.Buffer.Span);
            Assert.False(result.IsCompleted);
            reader.Advance(result.Buffer.Length);
        }

        var result2 = reader.Read();
        Assert.Empty(result2.Buffer.ToArray());
        Assert.True(result2.IsCompleted);
    }


    private static readonly string _data =
        new string('a', 20) +
        new string('b', 20) +
        new string('c', 20) +
        new string('d', 20) +
        new string('e', 20) +
        new string('f', 20) +
        new string('g', 20);
}
