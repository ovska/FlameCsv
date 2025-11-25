using FlameCsv.Utilities;

namespace FlameCsv.Tests.Utilities;

public class WritableBufferTests
{
    [Fact]
    public void Should_Write_And_Return_Buffers()
    {
        using var pool = new ReturnTrackingBufferPool();
        var buffer = new WritableBuffer<char>(pool);

        buffer.Push("test");
        Assert.Equal(1, buffer.Length);
        Assert.Equal("test", buffer[0].ToString());

        buffer.Push(new string('x', 512));
        Assert.Equal(2, buffer.Length);
        Assert.Equal("test", buffer[0].ToString());
        Assert.Equal(new string('x', 512), buffer[1].ToString());

        buffer.Clear();
        Assert.Equal(0, buffer.Length);
        // ReSharper disable once AccessToDisposedClosure
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => buffer[0]);

        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => buffer.Length);
        Assert.Throws<ObjectDisposedException>(() => buffer[0]);
        Assert.Throws<ObjectDisposedException>(() => buffer.Push(""));
        Assert.Throws<ObjectDisposedException>(() => buffer.Clear());
    }
}
