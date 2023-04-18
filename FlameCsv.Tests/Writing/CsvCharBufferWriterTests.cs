using System.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public sealed class CsvCharBufferWriterTests : IAsyncDisposable
{
    private CsvCharBufferWriter _writer;
    private StringWriter? _textWriter;

    private string Written => _textWriter?.ToString() ?? string.Empty;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _writer.CompleteAsync(null);

        if (_textWriter is not null)
            await _textWriter.DisposeAsync();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(16, false)]
    [InlineData(24, false)]
    [InlineData(27, false)]
    [InlineData(28, true)]
    [InlineData(32, true)]
    [InlineData(1024, true)]
    public void Should_Return_If_Needs_Flush(int written, bool expected)
    {
        Initialize(bufferSize: 32);
        _ = _writer.GetSpan(written);
        _writer.Advance(written);
        Assert.Equal(_writer.NeedsFlush, expected);
    }

    [Fact]
    public static void Should_Validate_Constructor_Params()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CsvCharBufferWriter(null!, null));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CsvCharBufferWriter(new StringWriter(), null, initialBufferSize: -1));
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

        "Hello".CopyTo(_writer.GetSpan());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Memory()
    {
        Initialize();

        "Hello".AsMemory().CopyTo(_writer.GetMemory());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Consecutive()
    {
        Initialize();

        "Foo".CopyTo(_writer.GetSpan());
        _writer.Advance("Foo".Length);

        "Bar".CopyTo(_writer.GetSpan());
        _writer.Advance("Bar".Length);

        "Xyz".CopyTo(_writer.GetSpan());
        _writer.Advance("Xyz".Length);

        await _writer.CompleteAsync(null);

        Assert.Equal("FooBarXyz", Written);
    }

    [Fact]
    public async Task Should_Resize_Buffer()
    {
        Initialize(bufferSize: 32);

        string x28 = new('x', 28);
        x28.CopyTo(_writer.GetSpan());
        _writer.Advance(x28.Length);

        x28.CopyTo(_writer.GetSpan(28));
        _writer.Advance(x28.Length);

        await _writer.CompleteAsync(null);

        Assert.Equal(x28 + x28, Written);
    }

    [Fact]
    public async Task Should_Resize_Buffer_Async()
    {
        Initialize(bufferSize: 32);

        string x28 = new('x', 28);
        x28.CopyTo(_writer.GetSpan());
        _writer.Advance(x28.Length);

        string y64 = new('y', 64);
        var dst = _writer.GetMemory(y64.Length);
        y64.CopyTo(dst.Span);
        _writer.Advance(y64.Length);

        await _writer.CompleteAsync(null);

        Assert.Equal(x28 + y64, Written);
    }

    [Fact]
    public async Task Should_Throw_On_Cancelled()
    {
        Initialize();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _writer.CompleteAsync(null, new CancellationToken(canceled: true));
        });
    }

    [Fact]
    public async Task Should_Not_Overwrite_Exception_On_Cancelled()
    {
        Initialize();

        await Assert.ThrowsAsync<UnreachableException>(async () =>
        {
            await _writer.CompleteAsync(new UnreachableException(), new CancellationToken(canceled: true));
        });
    }

    private void Initialize(int bufferSize = 1024)
    {
        _writer = new CsvCharBufferWriter(
            _textWriter = new StringWriter(),
            AllocatingArrayPool<char>.Instance,
            bufferSize);
    }
}
