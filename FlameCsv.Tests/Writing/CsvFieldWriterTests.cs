using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public sealed class CsvFieldWriterTests : IAsyncDisposable
{
    private CsvFieldWriter<char, CsvCharBufferWriter>? _writer;
    private StringWriter? _textWriter;

    private string Written => _textWriter?.ToString() ?? string.Empty;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_writer is not null)
        {
            await _writer.Writer.CompleteAsync(null);
        }

        if (_textWriter is not null)
            await _textWriter.DisposeAsync();
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

        _writer.WriteField(Formatter.Instance!, null);
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal("null", Written);
    }

    [Fact]
    public async Task Should_Escape_Headers()
    {
        Initialize();

        _writer.WriteText("header1");
        _writer.WriteDelimiter();
        _writer.WriteText("header,withcomma");
        await _writer.Writer.CompleteAsync(null);

        Assert.Equal("header1,\"header,withcomma\"", Written);
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

    [Theory, InlineData(true), InlineData(false)]
    public async Task Should_Escape_To_Extra_Buffer(bool escapeMode)
    {
        Initialize(CsvFieldEscaping.AlwaysQuote, bufferSize: 128, escapeMode ? '^' : null);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null);

        if (escapeMode)
        {
            Assert.Equal($"\"Test ^\"{new string('x', 114)}^\" test\"", Written);
        }
        else
        {
            Assert.Equal($"\"Test \"\"{new string('x', 114)}\"\" test\"", Written);
        }
    }

    [Theory, InlineData(-1), InlineData(int.MaxValue)]
    public void Should_Guard_Against_Broken_Formatters(int tokensWritten)
    {
        Initialize(CsvFieldEscaping.AlwaysQuote, bufferSize: 128);

        var formatter = new BrokenFormatter { Write = tokensWritten };

        Assert.Throws<InvalidOperationException>(() => _writer.WriteField(formatter, ""));
    }

    [Theory, InlineData(-1), InlineData((int)short.MaxValue)]
    public void Should_Guard_Against_Broken_Options(int tokensWritten)
    {
        var writer = CsvFieldWriter.Create(TextWriter.Null, new BrokenOptions
        {
            Delimiter = ',', Quote = '"', Newline = "\n", Write = tokensWritten
        });
        Assert.Throws<InvalidOperationException>(() => writer.WriteText("test"));
        writer.Writer.Complete(null);
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

    [MemberNotNull(nameof(_writer))]
    private void Initialize(
        CsvFieldEscaping quoting = CsvFieldEscaping.Auto,
        int bufferSize = 1024,
        char? escape = null)
    {
        _textWriter = new StringWriter();
        _writer = new CsvFieldWriter<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(_textWriter, AllocatingArrayPool<char>.Instance, bufferSize),
            new CsvOptions<char> { FieldEscaping = quoting, Null = "null", Escape = escape });
    }

    private sealed class Formatter : CsvConverter<char, string>
    {
        public static readonly Formatter Instance = new();

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotImplementedException();
        }

        public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
        {
            return value.AsSpan().TryWriteTo(destination, out charsWritten);
        }

        public override bool HandleNull => false;
    }

    private sealed class BrokenFormatter : CsvConverter<char, string>
    {
        public int Write { get; set; }

        public bool CanFormat(Type valueType)
            => throw new NotImplementedException();

        public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
        {
            charsWritten = Write;
            return true;
        }

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotImplementedException();
        }
    }

    private class BrokenOptions : CsvOptions<char>
    {
        public override bool TryWriteChars(ReadOnlySpan<char> value, Span<char> destination, out int charsWritten)
        {
            charsWritten = Write;
            return true;
        }

        public int Write { get; set; }
    }
}
