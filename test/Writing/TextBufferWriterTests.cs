using System.Diagnostics.CodeAnalysis;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests.Writing;

public sealed class TextBufferWriterTests : IAsyncDisposable
{
    private TextBufferWriter? _writer;
    private StringWriter? _textWriter;

    private string Written => _textWriter?.ToString() ?? string.Empty;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await using (_textWriter)
        {
            if (_writer is not null)
            {
                await _writer.CompleteAsync(null);
            }
        }
    }

    [Theory]
    [InlineData(256, false)]
    [InlineData(512, false)]
    [InlineData(1024, true)]
    [InlineData(4096, true)]
    public void Should_Return_If_Needs_Flush(int written, bool expected)
    {
        Initialize(bufferSize: 1024);
        _ = _writer.GetSpan(written);
        _writer.Advance(written);
        Assert.Equal(expected, _writer.NeedsFlush);
    }

    [Fact]
    public static void Should_Validate_Constructor_Params()
    {
        Assert.Throws<ArgumentNullException>(() => new TextBufferWriter(null!, HeapMemoryPool<char>.Instance, default));
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
        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Memory()
    {
        Initialize();

        "Hello".AsMemory().CopyTo(_writer.GetMemory());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

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

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

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

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

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

        await _writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(x28 + y64, Written);
    }

    [MemberNotNull(nameof(_writer))]
    private void Initialize(int bufferSize = 1024)
    {
        _writer = new TextBufferWriter(
            _textWriter = new StringWriter(),
            HeapMemoryPool<char>.Instance,
            new() { BufferSize = bufferSize }
        );
    }
}
