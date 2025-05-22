using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlameCsv.IO.Internal;
using Xunit;

namespace FlameCsv.Tests.IO.Internal;

public class EmptyBufferReaderTests
{
    [Fact]
    public void Should_Throw_If_Advance_Not_Zero()
    {
        var reader = EmptyBufferReader<byte>.Instance;
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Advance(1));
    }

    [Fact]
    public void Should_Be_NoOp()
    {
        var reader = EmptyBufferReader<byte>.Instance;

        reader.Dispose();
        Assert.True(reader.TryReset());
        Assert.Empty(reader.Read().Buffer.ToArray());
        Assert.True(reader.Read().IsCompleted);
    }

    [Fact]
    public async Task Should_Be_NoOp_Async()
    {
        var reader = EmptyBufferReader<byte>.Instance;
        await reader.DisposeAsync();
        Assert.Empty((await reader.ReadAsync(TestContext.Current.CancellationToken)).Buffer.ToArray());
        Assert.True((await reader.ReadAsync(TestContext.Current.CancellationToken)).IsCompleted);
    }
}
