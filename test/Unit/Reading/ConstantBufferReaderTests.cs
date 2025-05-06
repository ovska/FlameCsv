using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Reading;

public static class ConstantBufferReaderTests
{
    [Fact]
    public static void Should_Only_Use_MemorySteam()
    {
        Impl(Stream.Null);
        Impl(new MemoryStream(new byte[1], 0, 1, writable: false, publiclyVisible: false));
        Impl(new BufferedStream(Stream.Null));

        static void Impl(Stream stream)
        {
            using (stream)
            using (var pipe = CsvBufferReader.Create(
                       stream,
                       HeapMemoryPool<byte>.Instance,
                       new() { LeaveOpen = true }))
            {
                Assert.IsNotType<ConstantBufferReader<byte>>(pipe);
            }
        }
    }

    private class DerivedStringReader : StringReader
    {
        public DerivedStringReader(string s) : base(s)
        {
        }
    }

    [Fact]
    public static void Should_Only_Use_StringReader()
    {
        Impl(TextReader.Null);
        Impl(new StreamReader(Stream.Null));
        Impl(new DerivedStringReader(""));

        static void Impl(TextReader reader)
        {
            using (reader)
            using (var pipe = CsvBufferReader.Create(reader, HeapMemoryPool<char>.Instance, new() { BufferSize = 4096 }))
            {
                Assert.IsNotType<ConstantBufferReader<char>>(pipe);
            }
        }
    }

    [Fact]
    public static async Task Should_Advance_Stream()
    {
        using var pool = ReturnTrackingMemoryPool<byte>.Create();

        await using MemoryStream stream = new();
        stream.Write("Hello, World!"u8);
        stream.Position = 0;

        await using (var reader = CsvBufferReader.Create(stream, pool, new() { LeaveOpen = true }))
        {
            Assert.IsType<ConstantBufferReader<byte>>(reader);

            Assert.Equal(0, stream.Position);

            var result = await reader.ReadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(13, result.Buffer.Length);
            Assert.True(result.IsCompleted);
            Assert.Equal(13, stream.Position);

            // read again
            reader.Advance(result.Buffer.Length);
            result = await reader.ReadAsync(TestContext.Current.CancellationToken);

            Assert.True(result.IsCompleted);
            Assert.Empty(result.Buffer.ToArray());
            Assert.Equal(13, stream.Position);
        }

        // disposing the pipereader should not close the stream
        stream.Write("Test"u8);
    }

    [Theory, InlineData(0), InlineData(5)]
    public static async Task Should_Advance_TextReader(int pos)
    {
        using var pool = ReturnTrackingMemoryPool<char>.Create();

        using var reader = new StringReader("Hello, World!");
        Assert.Equal(pos, reader.Read(new char[pos]));

        await using var pipeReader = CsvBufferReader.Create(reader, pool, new() { LeaveOpen = true });

        Assert.IsType<ConstantBufferReader<char>>(pipeReader);

        var result = await pipeReader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(13 - pos, result.Buffer.Length);
        Assert.True(result.IsCompleted);
        Assert.Equal("Hello, World!"[pos..], new string(result.Buffer.ToArray()));

        // read again
        pipeReader.Advance(result.Buffer.Length);
        result = await pipeReader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsCompleted);
        Assert.Empty(result.Buffer.ToArray());
    }
}
