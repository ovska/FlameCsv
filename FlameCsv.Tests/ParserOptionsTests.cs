using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Tests;

public static class ParserOptionsTests
{
    [Fact]
    public static void Should_Validate()
    {
        AssertInvalid(default);

        var o = CsvTokens<char>.Windows;
        AssertInvalid(o with { StringDelimiter = ',' });
        AssertInvalid(o with { Whitespace = ",".AsMemory() });
        AssertInvalid(o with { Whitespace = "\n".AsMemory() });
        AssertInvalid(o with { Whitespace = "\"".AsMemory() });
        AssertInvalid(o with { NewLine = default });
        AssertInvalid(o with { NewLine = ",".AsMemory() });
        AssertInvalid(o with { NewLine = "\"".AsMemory() });
        AssertInvalid(o with { Delimiter = '\n' });

        Assert.Null(Record.Exception(() => CsvTokens<char>.Windows.ThrowIfInvalid()));

        static void AssertInvalid(CsvTokens<char> options)
        {
            Assert.Throws<CsvConfigurationException>(() => options.ThrowIfInvalid());
        }
    }

    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal(Environment.NewLine, CsvTokens<char>.Environment.NewLine.ToArray());
        Assert.Equal("\n", CsvTokens<char>.Unix.NewLine.ToArray());
        Assert.Equal("\r\n", CsvTokens<char>.Windows.NewLine.ToArray());

        Assert.Equal(Encoding.UTF8.GetBytes(Environment.NewLine), CsvTokens<byte>.Environment.NewLine.ToArray());
        Assert.Equal("\n"u8.ToArray(), CsvTokens<byte>.Unix.NewLine.ToArray());
        Assert.Equal("\r\n"u8.ToArray(), CsvTokens<byte>.Windows.NewLine.ToArray());

        Assert.Throws<NotSupportedException>(() => CsvTokens<int>.Environment);
        Assert.Throws<NotSupportedException>(() => CsvTokens<int>.Windows);
        Assert.Throws<NotSupportedException>(() => CsvTokens<int>.Unix);
    }

    [Fact]
    public static void Should_Implement_IEquatable()
    {
        var o1 = new CsvTokens<char>();
        var o2 = new CsvTokens<char>();
        ShouldEqual();

        o1 = o1 with { NewLine = Environment.NewLine.AsMemory() };
        ShouldNotEqual();
        o2 = o2 with { NewLine = Environment.NewLine.AsMemory() };
        ShouldEqual();

        o1 = o1 with { Delimiter = ',' };
        ShouldNotEqual();
        o2 = o2 with { Delimiter = ',' };
        ShouldEqual();

        o1 = o1 with { StringDelimiter = '"' };
        ShouldNotEqual();
        o2 = o2 with { StringDelimiter = '"' };
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
