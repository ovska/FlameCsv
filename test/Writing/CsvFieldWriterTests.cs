using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv.Tests.Writing;

public sealed class CsvFieldWriterTests : IAsyncDisposable
{
    [HandlesResourceDisposal]
    private CsvFieldWriter<char> _writer;
    private StringWriter? _textWriter;

    private string Written => _textWriter?.ToString() ?? string.Empty;

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await using (_textWriter)
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
    public async Task Should_Escape_Headers()
    {
        Initialize();

        _writer.WriteText("header1");
        _writer.WriteDelimiter();
        _writer.WriteText("header,withcomma");
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("header1,\"header,withcomma\"", Written);
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

    [Theory, InlineData(true), InlineData(false)]
    public async Task Should_Escape_To_Extra_Buffer(bool escapeMode)
    {
        Initialize(CsvFieldQuoting.Always, bufferSize: 128, escapeMode ? '^' : null);

        // 126, raw value can be written but escaped is 130 long
        var value = $"Test \"{new string('x', 114)}\" test";

        _writer.WriteField(Formatter.Instance, value);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(
            escapeMode
                ? $"\"Test ^\"{new string('x', 114)}^\" test\""
                : $"\"Test \"\"{new string('x', 114)}\"\" test\"",
            Written
        );
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
    [InlineData(CsvFieldQuoting.Auto, "x", "x")]
    [InlineData(CsvFieldQuoting.Auto, " ", " ")]
    [InlineData(CsvFieldQuoting.Empty, "", "\"\"")]
    [InlineData(CsvFieldQuoting.Empty, " ", " ")]
    [InlineData(CsvFieldQuoting.Empty, ",", ",")]
    [InlineData(CsvFieldQuoting.Empty, "x", "x")]
    [InlineData(CsvFieldQuoting.Empty | CsvFieldQuoting.Auto, ",", "\",\"")]
    [InlineData(CsvFieldQuoting.Always, "", "\"\"")]
    [InlineData(CsvFieldQuoting.Always, ",", "\",\"")]
    [InlineData(CsvFieldQuoting.Always, "x", "\"x\"")]
    [InlineData(CsvFieldQuoting.Always, " ", "\" \"")]
    [InlineData(CsvFieldQuoting.Never, "", "")]
    [InlineData(CsvFieldQuoting.Never, ",", ",")]
    [InlineData(CsvFieldQuoting.Never, "x", "x")]
    [InlineData(CsvFieldQuoting.Never, " ", " ")]
    [InlineData(CsvFieldQuoting.LeadingSpaces, " test", "\" test\"")]
    [InlineData(CsvFieldQuoting.LeadingSpaces, "test ", "test ")]
    [InlineData(CsvFieldQuoting.LeadingSpaces, " test ", "\" test \"")]
    [InlineData(CsvFieldQuoting.TrailingSpaces, " test", " test")]
    [InlineData(CsvFieldQuoting.TrailingSpaces, "test ", "\"test \"")]
    [InlineData(CsvFieldQuoting.TrailingSpaces, " test ", "\" test \"")]
    [InlineData(CsvFieldQuoting.LeadingOrTrailingSpaces, " test", "\" test\"")]
    [InlineData(CsvFieldQuoting.LeadingOrTrailingSpaces, "test ", "\"test \"")]
    [InlineData(CsvFieldQuoting.LeadingOrTrailingSpaces, " test ", "\" test \"")]
    public async Task Should_Quote_Fields(CsvFieldQuoting quoting, string input, string expected)
    {
        Initialize(quoting);

        _writer.WriteField(Formatter.Instance, input);
        await _writer.Writer.CompleteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(expected, Written);
    }

    [MemberNotNull(nameof(_writer))]
    private void Initialize(CsvFieldQuoting quoting = CsvFieldQuoting.Auto, int bufferSize = 1024, char? escape = null)
    {
        _textWriter = new StringWriter();
        _writer = new CsvFieldWriter<char>(
            new TextBufferWriter(_textWriter, HeapMemoryPool<char>.Instance, new() { BufferSize = bufferSize }),
            new CsvOptions<char>
            {
                FieldQuoting = quoting,
                Null = "null",
                Escape = escape,
            }
        );
    }

    private sealed class Formatter : CsvConverter<char, string>
    {
        public static readonly Formatter Instance = new();

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotSupportedException();
        }

        public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
        {
            return value.AsSpan().TryCopyTo(destination, out charsWritten);
        }

        protected internal override bool CanFormatNull => false;
    }

    private sealed class BrokenFormatter : CsvConverter<char, string>
    {
        public int Write { get; set; }

        public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
        {
            charsWritten = Write;
            return true;
        }

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotSupportedException();
        }
    }
}
