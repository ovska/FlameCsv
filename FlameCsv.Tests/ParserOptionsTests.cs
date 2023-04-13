using FlameCsv.Exceptions;

namespace FlameCsv.Tests;

public static class ParserOptionsTests
{
    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<CsvConfigurationException>(() => default(CsvDialect<char>).Clone());

        AssertInvalid(o => o.Clone(quote: ','));
        AssertInvalid(o => o.Clone(newline: ReadOnlyMemory<char>.Empty));
        AssertInvalid(o => o.Clone(newline: ",".AsMemory()));
        AssertInvalid(o => o.Clone(newline: "\"".AsMemory()));
        AssertInvalid(o => o.Clone(delimiter: '\n'));

        static void AssertInvalid(Action<CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() => action(CsvDialect<char>.Default));
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

        o1 = o1.Clone(newline: "xyz".AsMemory());
        ShouldNotEqual();
        o2 = o2.Clone(newline: "xyz".AsMemory());
        ShouldEqual();

        o1 = o1.Clone(delimiter: '^');
        ShouldNotEqual();
        o2 = o2.Clone(delimiter: '^');
        ShouldEqual();

        o1 = o1.Clone(quote: '_');
        ShouldNotEqual();
        o2 = o2.Clone(quote: '_');
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
