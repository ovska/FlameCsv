using System.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Reading;

namespace FlameCsv.Tests;

public static class DialectTests
{
    [Fact]
    public static void Should_Check_For_Null()
    {
        Impl(d => d with { Delimiter = '\0' });
        Impl(d => d with { Quote = '\0' });
        Impl(d => d with { Escape = '\0' });

        void Impl(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() => action(CsvOptions<char>.Default.Dialect).Validate());
        }
    }

    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal(',', CsvOptions<char>.Default.Dialect.Delimiter);
        Assert.Equal('"', CsvOptions<char>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<char>.Default.Dialect.Escape);
        Assert.Equal(CsvNewline.CRLF, CsvOptions<char>.Default.Dialect.Newline);
        Assert.Equal(CsvFieldTrimming.None, CsvOptions<char>.Default.Dialect.Trimming);

        Assert.Equal((byte)',', CsvOptions<byte>.Default.Dialect.Delimiter);
        Assert.Equal((byte)'"', CsvOptions<byte>.Default.Dialect.Quote);
        Assert.Null(CsvOptions<byte>.Default.Dialect.Escape);
        Assert.Equal(CsvNewline.CRLF, CsvOptions<byte>.Default.Dialect.Newline);
        Assert.Equal(CsvFieldTrimming.None, CsvOptions<byte>.Default.Dialect.Trimming);
    }

    [Fact]
    public static void Should_Ensure_No_Spaces_If_Trimmed()
    {
        GetDialect().Validate();
        Assert.Throws<CsvConfigurationException>(
            () => (GetDialect() with { Trimming = CsvFieldTrimming.Both }).Validate()
        );

        static CsvDialect<char> GetDialect() => new() { Delimiter = ' ', Quote = '"' };
    }

    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<InvalidOperationException>(() => default(CsvDialect<char>).Validate());

        AssertInvalid(o => o with { Quote = ',' });
        AssertInvalid(o => o with { Quote = '\r' });
        AssertInvalid(o => o with { Quote = '\n' });
        AssertInvalid(o => o with { Delimiter = '"' });
        AssertInvalid(o => o with { Delimiter = '\n' });
        AssertInvalid(o => o with { Delimiter = '\r' });
        AssertInvalid(o => o with { Escape = ',' });
        AssertInvalid(o => o with { Escape = '"' });
        AssertInvalid(o => o with { Escape = '\n' });
        AssertInvalid(o => o with { Escape = '\r' });

        static void AssertInvalid(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() =>
            {
                action(new CsvOptions<char>().Dialect).Validate();
            });
        }

        // that all searchable tokens must be ascii (no multibyte characters)
        ShouldThrow(() => new CsvOptions<char> { Delimiter = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<char> { Quote = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<char> { Escape = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<char> { Newline = (CsvNewline)byte.MaxValue }.Dialect.Validate());

        ShouldThrow(() => new CsvOptions<byte> { Delimiter = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Quote = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Escape = (char)128 }.Dialect.Validate());
        ShouldThrow(() => new CsvOptions<byte> { Newline = (CsvNewline)byte.MaxValue }.Dialect.Validate());

        static void ShouldThrow(Action action) => Assert.ThrowsAny<ArgumentException>(action);
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

        var withSingleTokenNewline = new CsvOptions<char> { Newline = CsvNewline.LF }.Dialect;
        Assert.True(withSingleTokenNewline.NeedsQuoting.Contains('\n'));
        Assert.False(withSingleTokenNewline.NeedsQuoting.Contains('\r'));
    }

    [Fact]
    public static void Should_Validate_Surrogates()
    {
        Validate(o => o with { Delimiter = '\uD800' });
        Validate(o => o with { Quote = '\uD800' });
        Validate(o => o with { Escape = '\uD800' });

        static void Validate(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() => action(CsvOptions<char>.Default.Dialect).Validate());
        }
    }

    [Fact]
    public static void Should_Override_NeedsQuoting()
    {
        var sv = SearchValues.Create("test".AsSpan());
        var dialect = new CsvDialect<char>
        {
            NeedsQuoting = sv,
            Delimiter = ',',
            Quote = '"',
        };
        Assert.Same(sv, dialect.NeedsQuoting);
    }

    [Fact]
    public static void Should_Equal()
    {
        var def = CsvOptions<char>.Default.Dialect;
        var def2 = CsvOptions<char>.Default.Dialect;
        var withEscape = new CsvOptions<char> { Escape = '\\' }.Dialect;
        var withNewline = new CsvOptions<char> { Newline = CsvNewline.LF }.Dialect;
        var withTrimming = new CsvOptions<char> { Trimming = CsvFieldTrimming.Both }.Dialect;

        Assert.Equal(def, def2);
        Assert.NotEqual(def, withEscape);
        Assert.NotEqual(def, withNewline);
        Assert.NotEqual(def, withTrimming);

        // ReSharper disable once RedundantWithExpression
        Assert.Equal(def, def with { });

        Assert.Equal(def.GetHashCode(), def2.GetHashCode());
        Assert.NotEqual(def.GetHashCode(), withEscape.GetHashCode());
        Assert.NotEqual(def.GetHashCode(), withNewline.GetHashCode());
        Assert.NotEqual(def.GetHashCode(), withTrimming.GetHashCode());

        Assert.True(def.Equals((object)def2));
        Assert.False(def.Equals(new object()));
        Assert.True(def == def2);
        Assert.False(def != def2);
    }
}
