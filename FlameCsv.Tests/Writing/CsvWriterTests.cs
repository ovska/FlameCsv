using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Writers;

namespace FlameCsv.Tests.Writing;

public sealed class CsvWriterTests : IAsyncDisposable
{
    private CsvWriteOperation<char, CsvCharBufferWriter>? _writer;
    private StringWriter? _textWriter;

    private string Written => _textWriter?.ToString() ?? string.Empty;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_writer is not null)
            await _writer.DisposeAsync();

        if (_textWriter is not null)
            await _textWriter.DisposeAsync();
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
    public async Task Should_Write_Null()
    {
        Initialize();

        await _writer.WriteValueAsync(Formatter.Instance!, null, default);
        await _writer.DisposeAsync();

        Assert.Equal("null", Written);
    }

    [Fact]
    public async Task Should_Write_Oversized_Value()
    {
        Initialize(quoting: CsvFieldQuoting.Never, bufferSize: 4);

        var value = new string('x', 500);

        await _writer.WriteValueAsync(Formatter.Instance, value, default);
        await _writer.DisposeAsync();

        Assert.Equal(value, Written);
    }

    [Fact]
    public async Task Should_Escape_To_Extra_Buffer()
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        await _writer.WriteValueAsync(Formatter.Instance, value, default);
        await _writer.DisposeAsync();

        Assert.Equal($"\"Test \"\"{new string('x', 114)}\"\" test\"", Written);
    }

    [Theory, InlineData(-1), InlineData(int.MaxValue)]
    public async Task Should_Guard_Against_Broken_Formatters(int tokensWritten)
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128);

        var formatter = new BrokenFormatter { Write = tokensWritten };

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _writer.WriteValueAsync(formatter, "", default));
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

        await _writer.WriteValueAsync(Formatter.Instance, input, default);
        await _writer.DisposeAsync();

        Assert.Equal(expected, Written);
    }

    [MemberNotNull(nameof(_writer))]
    private void Initialize(
        CsvFieldQuoting quoting = CsvFieldQuoting.Auto,
        int bufferSize = 1024)
    {
        _textWriter = new StringWriter();
        _writer = new CsvWriteOperation<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(_textWriter, AllocatingArrayPool<char>.Instance, bufferSize),
            new CsvWriterOptions<char> { FieldQuoting = quoting, Null = "null".AsMemory() });
    }

    private sealed class Formatter : ICsvFormatter<char, string>
    {
        public static readonly Formatter Instance = new();

        public bool CanFormat(Type valueType)
            => throw new NotImplementedException();

        public bool TryFormat(string value, Span<char> destination, out int tokensWritten)
            => value.AsSpan().TryWriteTo(destination, out tokensWritten);

        public bool HandleNull => false;
    }

    private sealed class BrokenFormatter : ICsvFormatter<char, string>
    {
        public int Write { get; set; }

        public bool CanFormat(Type valueType)
            => throw new NotImplementedException();

        public bool TryFormat(string value, Span<char> destination, out int tokensWritten)
        {
            tokensWritten = Write;
            return true;
        }
    }
}
