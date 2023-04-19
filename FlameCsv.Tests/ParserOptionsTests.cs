using FlameCsv.Exceptions;

namespace FlameCsv.Tests;

public static class ParserOptionsTests
{
    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<CsvConfigurationException>(() => default(CsvDialect<char>).EnsureValid());

        AssertInvalid(o => o with { Quote = ',' });
        AssertInvalid(o => o with { Newline = ReadOnlyMemory<char>.Empty });
        AssertInvalid(o => o with { Newline = ",".AsMemory() });
        AssertInvalid(o => o with { Newline = "\"".AsMemory() });
        AssertInvalid(o => o with { Delimiter = '\n' });
        AssertInvalid(o => o with { Escape = ',' });

        static void AssertInvalid(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() =>
            {
                var d = action(CsvDialect<char>.Default);
                _ = new CsvDialect<char>(d.Delimiter, d.Quote, d.Newline, d.Escape);
            });
        }
    }

    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal("\r\n", CsvDialect<char>.Default.Newline.ToArray());
        Assert.Equal("\r\n"u8.ToArray(), CsvDialect<byte>.Default.Newline.ToArray());
        Assert.Throws<NotSupportedException>(() => CsvDialect<int>.Default);
        Assert.Throws<NotSupportedException>(() => CsvDialect<long>.Default);
    }

    [Fact]
    public static void Should_Implement_IEquatable()
    {
        var o1 = CsvDialect<char>.Default;
        var o2 = CsvDialect<char>.Default;
        ShouldEqual();

        o1 = o1 with { Newline = "xyz".AsMemory() };
        ShouldNotEqual();
        o2 = o2 with { Newline = "xyz".AsMemory() };
        ShouldEqual();

        o1 = o1 with { Delimiter = '^' };
        ShouldNotEqual();
        o2 = o2 with { Delimiter = '^' };
        ShouldEqual();

        o1 = o1 with { Quote = '_' };
        ShouldNotEqual();
        o2 = o2 with { Quote = '_' };
        ShouldEqual();

        void ShouldNotEqual()
        {
            Assert.NotEqual(o1, o2);
            Assert.NotEqual(o1.GetHashCode(), o2.GetHashCode());
        }

        void ShouldEqual()
        {
            Assert.Equal(o1, o2);
            Assert.Equal(o1.GetHashCode(), o2.GetHashCode());
        }
    }
}
