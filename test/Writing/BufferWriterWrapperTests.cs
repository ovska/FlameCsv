using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests.Writing;

public class BufferWriterWrapperTests
{
    [Fact]
    public async Task Should_Write()
    {
        using var writer = new ArrayPoolBufferWriter<char>();
        var wrapper = new BufferWriterWrapper<char>(writer, null!);

        Assert.False(wrapper.NeedsDrain);

        "foo".CopyTo(wrapper.GetSpan(3));
        wrapper.Advance(3);

        Assert.Equal("foo", writer.WrittenSpan.ToString());

        "bar".CopyTo(wrapper.GetMemory(3).Span);
        wrapper.Advance(3);

        Assert.Equal("foobar", writer.WrittenSpan.ToString());

        wrapper.Drain();
        Assert.Equal("foobar", writer.WrittenSpan.ToString());

        await wrapper.DrainAsync(TestContext.Current.CancellationToken);
        Assert.Equal("foobar", writer.WrittenSpan.ToString());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            wrapper.DrainAsync(new CancellationToken(true)).AsTask()
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            wrapper.CompleteAsync(null, new CancellationToken(true)).AsTask()
        );

        wrapper.Complete(null);
        await wrapper.CompleteAsync(null, TestContext.Current.CancellationToken);
    }
}
