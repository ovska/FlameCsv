using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.IO;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Writing;

public sealed class PipeBufferWriterTests : IAsyncDisposable
{
    private PipeBufferWriter? _writer;
    private MemoryStream _memoryStream = null!;

    private string Written => Encoding.UTF8.GetString(_memoryStream.ToArray());

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await using (_memoryStream.ConfigureAwait(false))
        {
            if (_writer is not null)
                await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);
        }
    }

    private const int WindowsFlushThreshold = (int)(4096 * 15d / 16d);

    [Theory]
    [InlineData(0, false)]
    [InlineData(WindowsFlushThreshold - 1, false)]
    [InlineData(WindowsFlushThreshold, true)]
    [InlineData(WindowsFlushThreshold + 100, true)]
    public void Should_Return_If_Needs_Flush(int written, bool expected)
    {
        Initialize();
        _ = _writer.GetSpan(written);
        _writer.Advance(written);
        Assert.Equal(_writer.NeedsFlush, expected);
    }

    [Fact]
    public static void Should_Validate_Constructor_Params()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TextBufferWriter(null!, HeapMemoryPool<char>.Instance, 1024, false));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextBufferWriter(new StringWriter(), HeapMemoryPool<char>.Instance, bufferSize: 0, false));
    }

    [Fact]
    public void Should_Validate_Advance_Param()
    {
        Initialize();

        Assert.Throws<ArgumentOutOfRangeException>(() => _writer.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _writer.Advance(int.MaxValue));
    }

    [Fact]
    public async Task Should_Write_Span()
    {
        Initialize();

        "Hello"u8.CopyTo(_writer.GetSpan());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Memory()
    {
        Initialize();

        "Hello"u8.ToArray().CopyTo(_writer.GetMemory());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Consecutive()
    {
        Initialize();

        "Foo"u8.CopyTo(_writer.GetSpan());
        _writer.Advance("Foo".Length);

        "Bar"u8.CopyTo(_writer.GetSpan());
        _writer.Advance("Bar".Length);

        "Xyz"u8.CopyTo(_writer.GetSpan());
        _writer.Advance("Xyz".Length);

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("FooBarXyz", Written);
    }

    [Fact]
    public async Task Should_Resize_Buffer()
    {
        Initialize(bufferSize: 32);

        string _x28 = new('x', 28);
        byte[] x28 = Encoding.UTF8.GetBytes(_x28);
        x28.CopyTo(_writer.GetSpan());
        _writer.Advance(x28.Length);

        x28.CopyTo(_writer.GetSpan(28));
        _writer.Advance(x28.Length);

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(_x28 + _x28, Written);
    }

    [Fact]
    public async Task Should_Resize_Buffer_Async()
    {
        Initialize(bufferSize: 32);

        string _x28 = new('x', 28);
        byte[] x28 = Encoding.UTF8.GetBytes(_x28);
        x28.CopyTo(_writer.GetSpan());
        _writer.Advance(x28.Length);

        string _y64 = new('y', 64);
        byte[] y64 = Encoding.UTF8.GetBytes(_y64);
        var dst = _writer.GetMemory(y64.Length);
        y64.CopyTo(dst.Span);
        _writer.Advance(y64.Length);

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(_x28 + _y64, Written);
    }

    [Fact]
    public async Task Should_Throw_On_Cancelled()
    {
        Initialize();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => { await _writer.CompleteAsync(null, new CancellationToken(canceled: true)); });
    }

    [Fact]
    public async Task Should_Not_Overwrite_Exception_On_Cancelled()
    {
        Initialize();

        await Assert.ThrowsAsync<UnreachableException>(
            async () =>
            {
                await _writer.CompleteAsync(new UnreachableException(), new CancellationToken(canceled: true));
            });
    }

    [MemberNotNull(nameof(_writer))]
    private void Initialize(int bufferSize = 1024)
    {
        _writer = new PipeBufferWriter(
            PipeWriter.Create(
                _memoryStream = new MemoryStream(),
                new StreamPipeWriterOptions(minimumBufferSize: bufferSize)));
    }
}
