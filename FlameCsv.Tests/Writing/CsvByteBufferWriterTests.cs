using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public sealed class CsvByteBufferWriterTests : IAsyncDisposable
{
    private CsvByteBufferWriter _writer;
    private MemoryStream _memoryStream = null!;

    private string Written => Encoding.UTF8.GetString(_memoryStream!.ToArray());

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _writer.CompleteAsync(null);
        await _memoryStream.DisposeAsync();
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

        "Hello"u8.CopyTo(_writer.GetSpan());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null);

        Assert.Equal("Hello", Written);
    }

    [Fact]
    public async Task Should_Write_Memory()
    {
        Initialize();

        "Hello"u8.ToArray().CopyTo(_writer.GetMemory());
        _writer.Advance("Hello".Length);
        await _writer.CompleteAsync(null);

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

        await _writer.CompleteAsync(null);

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

        await _writer.CompleteAsync(null);

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

        await _writer.CompleteAsync(null);

        Assert.Equal(_x28 + _y64, Written);
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
        _writer = new CsvByteBufferWriter(
            PipeWriter.Create(
                _memoryStream = new MemoryStream(),
                new StreamPipeWriterOptions(minimumBufferSize: bufferSize)));
    }
}
