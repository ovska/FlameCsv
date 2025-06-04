using System.Buffers;
using System.Text;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests.Writing;

public class StringBuilderBufferWriterTests
{
    [Fact]
    public void ShouldResizeBuffer()
    {
        var builder = new StringBuilder();
        var writer = new StringBuilderBufferWriter(builder, MemoryPool<char>.Shared);

        var memory = writer.GetMemory(100);
        "abc".AsSpan().CopyTo(memory.Span);
        writer.Advance(3);

        memory = writer.GetMemory(4096 + 1);
        Assert.True(4096 + 1 <= memory.Length);
        "defg".AsSpan().CopyTo(memory.Span);
        writer.Advance(4);

        writer.Flush();
        Assert.Equal("abcdefg", builder.ToString());

        writer.Complete(null);
    }

    [Fact]
    public void AdvanceAndGetMemory()
    {
        var builder = new StringBuilder();
        var writer = new StringBuilderBufferWriter(builder, MemoryPool<char>.Shared);

        var memory = writer.GetMemory(10);
        memory.Span.Fill('A');
        writer.Advance(10);

        Assert.Equal("AAAAAAAAAA", builder.ToString());

        var span = writer.GetSpan(5);
        span.Fill('B');

        // data is copied only when advancing
        Assert.Equal("AAAAAAAAAA", builder.ToString());

        writer.Advance(5);
        Assert.Equal("AAAAAAAAAABBBBB", builder.ToString());

        writer.Complete(null);
    }

    [Fact]
    public async Task FlushDoesNotThrow()
    {
        var builder = new StringBuilder();
        var writer = new StringBuilderBufferWriter(builder, MemoryPool<char>.Shared);

        Assert.False(writer.NeedsFlush);
        writer.Flush();
        await writer.FlushAsync(TestContext.Current.CancellationToken);
        Assert.Empty(builder.ToString());

        await writer.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ThrowsIfDisposed()
    {
        var writer = new StringBuilderBufferWriter(new(), MemoryPool<char>.Shared);
        writer.Complete(null);

        Assert.Throws<ObjectDisposedException>(() => writer.Advance(0));
        Assert.Throws<ObjectDisposedException>(() => writer.Flush());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await writer.FlushAsync(TestContext.Current.CancellationToken)
        );
    }
}
