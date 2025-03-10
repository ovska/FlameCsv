using System.Buffers;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.Utilities;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

file static class EscapeExt
{
    public static readonly CsvOptions<char> Options = new()
    {
        Delimiter = ',',
        Quote = '|',
    };

    public static bool MustBeQuoted(this RFC4180Escaper<char> escaper, ReadOnlySpan<char> field, out int specialCount)
    {
        if (field.IndexOfAny(Options.Dialect.NeedsQuoting) >= 0)
        {
            specialCount = escaper.CountEscapable(field);
            return true;
        }

        specialCount = 0;
        return false;
    }
}

public static class RFC4180EscapeTests
{
    private static readonly RFC4180Escaper<char> _escaper = new(quote: EscapeExt.Options.Quote);

    [Fact]
    public static void Should_Partial_Escape_Larger_Destination()
    {
        ReadOnlySpan<char> input = "|t|e|s|t|";

        Assert.True(_escaper.MustBeQuoted(input, out int specialCount));
        Assert.Equal(5, specialCount);

        using var arrayPool = new ReturnTrackingArrayMemoryPool<char>();
        var writer = new ArrayBufferWriter<char>();
        var destination = writer.GetSpan(14)[..14];
        input.CopyTo(destination);

        var escaper = _escaper;
        Escape.FieldWithOverflow(
            ref escaper,
            writer,
            source: destination[..input.Length],
            destination: destination,
            specialCount: specialCount,
            allocator: arrayPool);

        Assert.Equal("|||t||e||s||t|||", writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData(",test ", "|,test |")]
    [InlineData("test,", "|test,|")]
    [InlineData(",test", "|,test|")]
    [InlineData(" te|st ", "| te||st |")]
    [InlineData("|", "||||")]
    [InlineData("|t", "|||t|")]
    public static void Should_Partial_Escape(string input, string expected)
    {
        Assert.True(_escaper.MustBeQuoted(input, out int specialCount));

        var writer = new ArrayBufferWriter<char>();
        Span<char> firstBuffer = writer.GetSpan(input.Length)[..input.Length];
        input.CopyTo(firstBuffer);

        using var arrayPool = new ReturnTrackingArrayMemoryPool<char>();

        var escaper = _escaper;
        Escape.FieldWithOverflow(
            ref escaper,
            writer,
            source: firstBuffer,
            destination: firstBuffer,
            specialCount: specialCount,
            allocator: arrayPool);

        Assert.Equal(expected, writer.WrittenSpan.ToString());
    }

    [Theory]
    [InlineData("", "||")]
    [InlineData(" ", "| |")]
    [InlineData("test", "|test|")]
    public static void Should_Wrap_In_Quotes(string input, string expected)
    {
        Span<char> buffer = stackalloc char[expected.Length];
        input.CopyTo(buffer);

        var escaper = _escaper;
        Escape.Field(ref escaper, buffer[..input.Length], buffer, 0);
        Assert.Equal(expected, buffer.ToString());
    }

    [Theory]
    [InlineData(",test", "|,test|")]
    [InlineData("test,", "|test,|")]
    [InlineData("\r\ntest", "|\r\ntest|")]
    [InlineData("te|st", "|te||st|")]
    [InlineData("||", "||||||")]
    [InlineData("|", "||||")]
    [InlineData("|a|", "|||a|||")]
    [InlineData("a|a", "|a||a|")]
    public static void Should_Escape(string input, string expected)
    {
        Assert.True(_escaper.MustBeQuoted(input, out int quoteCount));

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        var escaper = _escaper;
        Escape.Field(ref escaper, sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
    }

    [Theory]
    [InlineData("\ntest", "|\ntest|")]
    [InlineData("\n|test|", "|\n||test|||")]
    [InlineData("test\n", "|test\n|")]
    [InlineData("\n", "|\n|")]
    public static void Should_Escape_1Char_Newline(string input, string expected)
    {
        var escaper = _escaper;
        int quoteCount = escaper.CountEscapable(input);

        int expectedLength = input.Length + quoteCount + 2;
        Assert.Equal(expected.Length, expectedLength);

        // Escaping should work even if source and destination are on the same buffer
        var sharedBuffer = new char[expectedLength].AsSpan();
        input.CopyTo(sharedBuffer);

        Escape.Field(ref escaper, sharedBuffer[..input.Length], sharedBuffer, quoteCount);
        Assert.Equal(expected, new string(sharedBuffer));

        // Last sanity check
        Assert.Equal(
            $"|{input.Replace("|", "||")}|",
            sharedBuffer.ToString());
    }

    private static readonly (int? quoteCount, string data)[] _needsEscapingData =
    [
        (null, "foobar"),
        (null, "foo bar"),
        (0, "foo,bar"),
        (1, "foo|bar"),
        (2, "|foobar|"),
        (1, "|"),
        (0, "\r\n"),
        (0, "\r\n,"),
        (0, "Really, really long input"),
        (2, "| test |"),
        (3, "||\r\n test |"),
    ];

    public static TheoryData<string, int?, string> NeedsEscapingData()
    {
        var values = from x in _needsEscapingData
                     from newline in new[] { "\r\n", "\n" }
                     select new { newline, x.quoteCount, x.data };

        var theory = new TheoryData<string, int?, string>();

        foreach (var x in values)
        {
            theory.Add(x.newline, x.quoteCount, x.data);
        }

        return theory;
    }

    [Theory, MemberData(nameof(NeedsEscapingData))]
    public static void Should_Check_Needs_Escaping(string newline, int? quotes, string input)
    {
        var options = new CsvOptions<char> { Quote = '|', Newline = newline };
        input = input.Replace("\r\n", newline);

        bool needsQuoting = input.AsSpan().ContainsAny(options.Dialect.NeedsQuoting);

        if (!quotes.HasValue)
        {
            Assert.False(needsQuoting);
        }
        else
        {
            Assert.True(needsQuoting);
            Assert.Equal(quotes.Value, _escaper.CountEscapable(input));
        }
    }

    [Fact]
    public static void Should_Escx()
    {
        Assert.True(Vec128Char.IsSupported);

        const string data = "James |007| Bond";
        char[] buffer = new char[32];

        data.CopyTo(buffer);
        ReadOnlySpan<char> value = buffer.AsSpan(0, data.Length);
        NewlineParserOne<char, Vec128Char> newlineParser = new('\n');
        uint[] bitbuffer = new uint[(data.Length + Vec128Char.Count - 1) / Vec128Char.Count];

        bool retVal = EscapeHandler.NeedsEscaping<char, NewlineParserOne<char, Vec128Char>, Vec128Char>(
            buffer,
            data.Length,
            bitbuffer,
            ',',
            '|',
            in newlineParser,
            out int quoteCount);

        Assert.True(retVal);
        Assert.Equal(2, quoteCount);

        // unescape to the same buffer
        Span<char> destination = buffer.AsSpan(0, value.Length + quoteCount);

        EscapeHandler.Escape(value, destination, bitbuffer.AsSpan(), '|');

        Assert.Equal(
            "James ||007|| Bond",
            new string(destination));

        Assert.All(
            buffer[destination.Length..],
            x => Assert.Equal('\0', x));
    }
}
