using System.Buffers;
using System.Diagnostics;
using FlameCsv.Exceptions;
#if !RELEASE
using FlameCsv.Reading.Internal;
#endif

namespace FlameCsv.Tests;

public static class DialectTests
{
    [Fact]
    public static void Should_Return_FindToken()
    {
        Assertions(CsvOptions<char>.Default, ",\"", 0);
        Assertions(CsvOptions<char>.Default, ",\"\n", 1);
        Assertions(CsvOptions<char>.Default, ",\"\r", 2);
        Assertions(new CsvOptions<char> { Escape = '^', }, ",\"^", 0);
        Assertions(new CsvOptions<char> { Escape = '^', }, ",\"^\n", 1);
        Assertions(new CsvOptions<char> { Escape = '^', }, ",\"^\r", 2);

        Assert.Throws<UnreachableException>(() => new CsvOptions<char> { Newline = "\r\n" }.Dialect.GetFindToken(0));
        Assert.Throws<IndexOutOfRangeException>(() => CsvOptions<char>.Default.Dialect.GetFindToken(-1));
        Assert.Throws<IndexOutOfRangeException>(() => CsvOptions<char>.Default.Dialect.GetFindToken(3));

        static void Assertions(CsvOptions<char> options, string expected, int newlineLength)
        {
            SearchValues<char> v = options.Dialect.GetFindToken(newlineLength);
            Assert.Empty(expected.Where(c => !v.Contains(c)));
        }
    }

    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal(',', CsvOptions<char>.Default.Dialect.Delimiter);
        Assert.Equal('"', CsvOptions<char>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<char>.Default.Dialect.Escape);
        Assert.Empty(CsvOptions<char>.Default.Dialect.Newline.ToArray());
        Assert.Empty(CsvOptions<char>.Default.Dialect.Whitespace.ToArray());

        Assert.Equal((byte)',', CsvOptions<byte>.Default.Dialect.Delimiter);
        Assert.Equal((byte)'"', CsvOptions<byte>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<byte>.Default.Dialect.Escape);
        Assert.Empty(CsvOptions<byte>.Default.Dialect.Newline.ToArray());
        Assert.Empty(CsvOptions<byte>.Default.Dialect.Whitespace.ToArray());
    }

    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<InvalidOperationException>(() => default(CsvDialect<char>).Validate());

        AssertInvalid(o => o with { Quote = ',' });
        AssertInvalid(o => o with { Newline = "," });
        AssertInvalid(o => o with { Newline = "\"" });
        AssertInvalid(o => o with { Delimiter = '\n' });
        AssertInvalid(o => o with { Escape = ',' });
        AssertInvalid(o => o with { Whitespace = "," });

        static void AssertInvalid(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() => { action(new CsvOptions<char>().Dialect).Validate(); });
        }

        // utf8 requires that all tokens be ascii (no multibyte characters)
        ShouldThrow(() => new CsvOptions<byte> { Delimiter = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Quote = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Escape = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Newline = "£€$" }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Whitespace = "£€$" }.Dialect.Validate());

        static void ShouldThrow(Action action) => Assert.Throws<CsvConfigurationException>(action);
    }

    [Fact]
    public static void Should_Initialize_EscapeValues()
    {
        var def = CsvOptions<char>.Default.Dialect;
        Assert.True(def.NeedsQuoting.Contains(def.Delimiter));
        Assert.True(def.NeedsQuoting.Contains(def.Quote));
        Assert.True(def.NeedsQuoting.Contains('\r'));
        Assert.True(def.NeedsQuoting.Contains('\n'));
        Assert.False(def.NeedsQuoting.Contains('a'));
        Assert.False(def.NeedsQuoting.Contains('\\'));

        var withEscape = new CsvOptions<char> { Escape = '\\' }.Dialect;
        Assert.True(withEscape.NeedsQuoting.Contains('\\'));

        var withSingleTokenNewline = new CsvOptions<char> { Newline = "\n" }.Dialect;
        Assert.True(withSingleTokenNewline.NeedsQuoting.Contains('\n'));
        Assert.False(withSingleTokenNewline.NeedsQuoting.Contains('\r'));
    }

    [Fact]
    public static void Should_Report_If_Ascii()
    {
        Assert.True(CsvOptions<byte>.Default.Dialect.IsAscii);
        Assert.True(CsvOptions<char>.Default.Dialect.IsAscii);

        var inDelimiter = new CsvOptions<char> { Delimiter = '€' }.Dialect;
        var inNewline = new CsvOptions<char> { Newline = "€" }.Dialect;

        Assert.False(inDelimiter.IsAscii);
        Assert.False(inNewline.IsAscii);

#if !RELEASE
        // vectorization requires ASCII
        Assert.True(SimdVector.IsSupported(in CsvOptions<byte>.Default.Dialect));
        Assert.True(SimdVector.IsSupported(in CsvOptions<char>.Default.Dialect));
        Assert.False(SimdVector.IsSupported(in inDelimiter));
        Assert.False(SimdVector.IsSupported(in inNewline));
#endif
    }
}
