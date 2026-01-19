using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv.Tests.Writing;

public sealed class CsvByteWriterTests : IAsyncDisposable
{
    [HandlesResourceDisposal]
    private CsvFieldWriter<byte> _writer = null!;
    private MemoryStream? _stream;

    private string Written =>
        _stream is not null && _stream.TryGetBuffer(out var buffer)
            ? Encoding.UTF8.GetString(buffer.AsSpan())
            : throw new UnreachableException();

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await using (_stream)
        using (_writer)
        {
            if (_writer.Writer is not null)
            {
                await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task Should_Write_Delimiter()
    {
        Initialize();

        _writer.WriteDelimiter();
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(",", Written);
    }

    [Fact]
    public async Task Should_Write_Newline()
    {
        Initialize();

        _writer.WriteNewline();
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("\r\n", Written);
    }

    [Fact]
    public async Task Should_Write_String()
    {
        Initialize();

        _writer.WriteText("Test");
        _writer.WriteText("");

        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("Test", Written);
    }

    [Fact]
    public async Task Should_Write_Null()
    {
        Initialize();

        _writer.WriteField(Formatter.Instance, null);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("null", Written);
    }

    [Fact]
    public async Task Should_Write_Oversized_Value()
    {
        Initialize(quoting: CsvFieldQuoting.Never, bufferSize: 4);

        var value = new string('x', 500);

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(value, Written);
    }

    [Fact]
    public async Task Should_Escape_To_Extra_Buffer()
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal($"\"Test \"\"{new string('x', 114)}\"\" test\"", Written);
    }

    [Theory, InlineData(-1), InlineData(int.MaxValue)]
    public void Should_Guard_Against_Broken_Formatters(int tokensWritten)
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        var formatter = new BrokenFormatter { Write = tokensWritten };

        Assert.Throws<InvalidOperationException>(() => _writer.WriteField(formatter, ""));
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

        _writer.WriteField(Formatter.Instance, input);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Written);
    }

    private void Initialize(CsvFieldQuoting quoting = CsvFieldQuoting.Auto, int bufferSize = 1024)
    {
        _stream = new MemoryStream();
        _writer = new CsvFieldWriter<byte>(
            new PipeBufferWriter(
                PipeWriter.Create(
                    _stream,
                    new StreamPipeWriterOptions(minimumBufferSize: bufferSize, pool: HeapMemoryPool<byte>.Instance)
                ),
                null
            ),
            new CsvOptions<byte> { FieldQuoting = quoting, Null = "null" }
        );
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

        protected internal override bool CanFormatNull => false;
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
