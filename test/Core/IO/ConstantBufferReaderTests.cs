using System.Buffers;
using System.Text;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

// ReSharper disable DisposeOnUsingVariable

namespace FlameCsv.Tests.IO;

public static class ConstantBufferReaderTests
{
    [Fact]
    public static void Should_Handle_Partials_Advances()
    {
        using var reader = new ConstantBufferReader<char>("test".AsMemory());
        var result = reader.Read();
        Assert.Equal("test", result.Buffer.ToString());
        Assert.True(result.IsCompleted);

        reader.Advance(2);

        result = reader.Read();
        Assert.Equal("st", result.Buffer.ToString());
        Assert.True(result.IsCompleted);

        reader.Advance(2);

        result = reader.Read();
        Assert.Empty(result.Buffer.ToArray());
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public static void Should_Validate_Arg()
    {
        using var reader = new ConstantBufferReader<char>("test".AsMemory());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Advance(5));

        reader.Dispose();
        reader.Dispose(); // multiple dispose should be safe

        Assert.Throws<ObjectDisposedException>(() => reader.Advance(0));
        Assert.Throws<ObjectDisposedException>(() => reader.TryReset());
    }

    [Fact]
    public static async Task Should_Throw_If_Cancellation_Token_Canceled()
    {
        await using var reader = new ConstantBufferReader<char>("test".AsMemory());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(new(canceled: true)).AsTask());

        await reader.DisposeAsync();
        await reader.DisposeAsync(); // multiple dispose should be safe

        await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.ReadAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public static void Should_Read_From_StringBuilder()
    {
        var data = new StringBuilder(capacity: 10)
            .Append('x', 500)
            .Append(new StringBuilder("test", capacity: 10))
            .Append(new StringBuilder(capacity: 10).Append('y', 500));

        using var reader = CsvBufferReader.Create(data);
        var result = reader.ReadToBuffer();

        Assert.Equal(new string('x', 500) + "test" + new string('y', 500), result.ToString());
    }

    [Fact]
    public static void Should_Return_Constant()
    {
        // empty data from any source should return the empty instance
        Assert.Same(EmptyBufferReader<char>.Instance, CsvBufferReader.Create(""));
        Assert.Same(EmptyBufferReader<char>.Instance, CsvBufferReader.Create(new StringBuilder()));
        Assert.Same(EmptyBufferReader<char>.Instance, CsvBufferReader.Create(ReadOnlyMemory<char>.Empty));
        Assert.Same(EmptyBufferReader<char>.Instance, CsvBufferReader.Create(new ReadOnlySequence<char>()));

        Assert.IsType<ConstantBufferReader<char>>(CsvBufferReader.Create("test".AsMemory()));

        // single segment sequences should use a buffer reader
        Assert.IsType<ConstantBufferReader<char>>(CsvBufferReader.Create(new ReadOnlySequence<char>(['a', 'b', 'c'])));
        Assert.IsType<ConstantBufferReader<char>>(CsvBufferReader.Create(new StringBuilder("abc")));

        // only fall back to sequence reader if we have to
        Assert.IsType<ConstantSequenceReader<char>>(CsvBufferReader.Create(MemorySegment.Create("abc", "xyz")));
        Assert.IsType<ConstantBufferReader<char>>(
            CsvBufferReader.Create(new StringBuilder("abc").Append(new StringBuilder("xyz")))
        );
    }

    [Fact]
    public static void Should_Only_Use_MemorySteam()
    {
        Impl(Stream.Null);
        Impl(new MemoryStream(new byte[1], 0, 1, writable: false, publiclyVisible: false));
        Impl(new BufferedStream(Stream.Null));

        static void Impl(Stream stream)
        {
            using (stream)
            using (
                var reader = CsvBufferReader.Create(stream, HeapMemoryPool<byte>.Instance, new() { LeaveOpen = true })
            )
            {
                Assert.IsNotType<ConstantBufferReader<byte>>(reader);
            }
        }
    }

    private class DerivedStringReader : StringReader
    {
        public DerivedStringReader(string s)
            : base(s) { }
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
            using (
                var pipe = CsvBufferReader.Create(reader, HeapMemoryPool<char>.Instance, new() { BufferSize = 4096 })
            )
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
