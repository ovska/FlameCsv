using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public sealed class CsvByteWriterTests : IAsyncDisposable
{
    private CsvFieldWriter<byte> _writer;
    private MemoryStream? _stream;

    private string Written
        => _stream is not null && _stream.TryGetBuffer(out var buffer)
            ? Encoding.UTF8.GetString(buffer.AsSpan())
            : throw new UnreachableException();

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await using (_stream)
        {
            if (_writer.Writer is not null)
            {
                await _writer.Writer.CompleteAsync(null);
            }
        }
    }

    [Fact]
    public async Task Should_Write_Delimiter()
    {
        Initialize();

        _writer.WriteDelimiter();
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal(",", Written);
    }

    [Fact]
    public async Task Should_Write_Newline()
    {
        Initialize();

        _writer.WriteNewline();
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal("\r\n", Written);
    }

    [Fact]
    public async Task Should_Write_String()
    {
        Initialize();

        _writer.WriteText("Test");
        _writer.WriteText("");

        await _writer.Writer.CompleteAsync(null);

        Assert.Equal("Test", Written);
    }

    [Fact]
    public async Task Should_Write_Null()
    {
        Initialize();

        _writer.WriteField(Formatter.Instance, null);
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal("null", Written);
    }

    [Fact]
    public async Task Should_Write_Oversized_Value()
    {
        Initialize(quoting: CsvFieldEscaping.Never, bufferSize: 4);

        var value = new string('x', 500);

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal(value, Written);
    }

    [Fact]
    public async Task Should_Escape_To_Extra_Buffer()
    {
        Initialize(CsvFieldEscaping.AlwaysQuote, bufferSize: 128);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal($"\"Test \"\"{new string('x', 114)}\"\" test\"", Written);
    }

    [Theory, InlineData(-1), InlineData(int.MaxValue)]
    public void Should_Guard_Against_Broken_Formatters(int tokensWritten)
    {
        Initialize(CsvFieldEscaping.AlwaysQuote, bufferSize: 128);

        var formatter = new BrokenFormatter { Write = tokensWritten };

        Assert.Throws<InvalidOperationException>(() => _writer.WriteField(formatter, ""));
    }

    [Theory]
    [InlineData(CsvFieldEscaping.Auto, "", "")]
    [InlineData(CsvFieldEscaping.Auto, ",", "\",\"")]
    [InlineData(CsvFieldEscaping.AlwaysQuote, "", "\"\"")]
    [InlineData(CsvFieldEscaping.AlwaysQuote, ",", "\",\"")]
    [InlineData(CsvFieldEscaping.Never, "", "")]
    [InlineData(CsvFieldEscaping.Never, ",", ",")]
    public async Task Should_Quote_Fields(CsvFieldEscaping quoting, string input, string expected)
    {
        Initialize(quoting);

        _writer.WriteField(Formatter.Instance, input);
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal(expected, Written);
    }

    private void Initialize(
        CsvFieldEscaping quoting = CsvFieldEscaping.Auto,
        int bufferSize = 1024)
    {
        _stream = new MemoryStream();
        _writer = new CsvFieldWriter<byte>(
            new CsvByteBufferWriter(
                PipeWriter.Create(
                    _stream,
                    new StreamPipeWriterOptions(minimumBufferSize: bufferSize, pool: HeapMemoryPool<byte>.Shared))),
            new CsvOptions<byte> { FieldEscaping = quoting, Null = "null" });
    }

    private sealed class Formatter : CsvConverter<byte, string>
    {
        public static readonly Formatter Instance = new();

        public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotSupportedException();
        }

        public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
        {
            return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
        }

        public override bool HandleNull => false;
    }

    private sealed class BrokenFormatter : CsvConverter<byte, string>
    {
        public int Write { get; set; }

        public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
        {
            charsWritten = Write;
            return true;
        }

        public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotSupportedException();
        }
    }
}
