using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public sealed class CsvByteWriterTests : IAsyncDisposable
{
    private CsvRecordWriter<byte, CsvByteBufferWriter>? _writer;
    private MemoryStream? _stream;

    private string Written => _stream is not null && _stream.TryGetBuffer(out var buffer)
        ? Encoding.UTF8.GetString(buffer.AsSpan())
        : throw new UnreachableException();

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_writer is not null)
            await _writer.DisposeAsync();

        if (_stream is not null)
            await _stream.DisposeAsync();
    }

    [Fact]
    public async Task Should_Write_Delimiter()
    {
        Initialize();

        _writer.WriteDelimiter();
        await _writer.DisposeAsync();

        Assert.Equal(",", Written);
    }

    [Fact]
    public async Task Should_Write_Newline()
    {
        Initialize();

        _writer.WriteNewline();
        await _writer.DisposeAsync();

        Assert.Equal("\r\n", Written);
    }

    [Fact]
    public async Task Should_Write_String()
    {
        Initialize();

        _writer.WriteString("Test");
        _writer.WriteString("");

        await _writer.DisposeAsync();

        Assert.Equal("Test", Written);
    }

    [Fact]
    public async Task Should_Write_Null()
    {
        Initialize();

        _writer.WriteValue(Formatter.Instance!, null);
        await _writer.DisposeAsync();

        Assert.Equal("null", Written);
    }

    [Fact]
    public async Task Should_Write_Oversized_Value()
    {
        Initialize(quoting: CsvFieldQuoting.Never, bufferSize: 4);

        var value = new string('x', 500);

        _writer.WriteValue(Formatter.Instance, value);
        await _writer.DisposeAsync();

        Assert.Equal(value, Written);
    }

    [Fact]
    public async Task Should_Escape_To_Extra_Buffer()
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        _writer.WriteValue(Formatter.Instance, value);
        await _writer.DisposeAsync();

        Assert.Equal($"\"Test \"\"{new string('x', 114)}\"\" test\"", Written);
    }

    [Theory, InlineData(-1), InlineData(int.MaxValue)]
    public void Should_Guard_Against_Broken_Formatters(int tokensWritten)
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        var formatter = new BrokenFormatter { Write = tokensWritten };

        Assert.Throws<InvalidOperationException>(() => _writer.WriteValue(formatter, ""));
    }

    [Theory]
    [InlineData(CsvFieldQuoting.Auto, "", "")]
    [InlineData(CsvFieldQuoting.Auto, ",", "\",\"")]
    [InlineData(CsvFieldQuoting.Always, "", "\"\"")]
    [InlineData(CsvFieldQuoting.Always, ",", "\",\"")]
    [InlineData(CsvFieldQuoting.Never, "", "")]
    [InlineData(CsvFieldQuoting.Never, ",", ",")]
    public async Task Should_Quote_Fields(CsvFieldQuoting quoting, string input, string expected)
    {
        Initialize(quoting);

        _writer.WriteValue(Formatter.Instance, input);
        await _writer.DisposeAsync();

        Assert.Equal(expected, Written);
    }

    [MemberNotNull(nameof(_writer))]
    private void Initialize(
        CsvFieldQuoting quoting = CsvFieldQuoting.Auto,
        int bufferSize = 1024)
    {
        _stream = new MemoryStream();
        _writer = new CsvRecordWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(PipeWriter.Create(_stream, new StreamPipeWriterOptions(minimumBufferSize: bufferSize, pool: new AllocatingMemoryPool()))),
            new CsvWriterOptions<byte> { FieldQuoting = quoting, Null = "null"u8.ToArray() });
    }

    private sealed class Formatter : ICsvFormatter<byte, string>
    {
        public static readonly Formatter Instance = new();

        public bool CanFormat(Type valueType)
            => throw new NotImplementedException();

        public bool TryFormat(string value, Span<byte> destination, out int tokensWritten)
            => value.AsSpan().TryWriteUtf8To(destination, out tokensWritten);

        public bool HandleNull => false;
    }

    private sealed class BrokenFormatter : ICsvFormatter<byte, string>
    {
        public int Write { get; set; }

        public bool CanFormat(Type valueType)
            => throw new NotImplementedException();

        public bool TryFormat(string value, Span<byte> destination, out int tokensWritten)
        {
            tokensWritten = Write;
            return true;
        }
    }

    private sealed class AllocatingMemoryPool : MemoryPool<byte>
    {
        public override int MaxBufferSize => Array.MaxLength;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            return MemoryOwner<byte>.Allocate(Math.Max(0, minBufferSize), AllocatingArrayPool<byte>.Instance);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
