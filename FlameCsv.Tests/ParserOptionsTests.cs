using System.Text;

namespace FlameCsv.Tests;

public static class ParserOptionsTests
{
#if !NET7_0_OR_GREATER
    private static byte[] U8(string s) => Encoding.UTF8.GetBytes(s);
#endif

    [Fact]
    public static void Should_Return_Default_Options()
    {
        Assert.Equal(Environment.NewLine, CsvParserOptions<char>.Environment.NewLine.ToArray());
        Assert.Equal("\n", CsvParserOptions<char>.Unix.NewLine.ToArray());
        Assert.Equal("\r\n", CsvParserOptions<char>.Windows.NewLine.ToArray());

        Assert.Equal(U8(Environment.NewLine), CsvParserOptions<byte>.Environment.NewLine.ToArray());
        Assert.Equal(U8("\n"), CsvParserOptions<byte>.Unix.NewLine.ToArray());
        Assert.Equal(U8("\r\n"), CsvParserOptions<byte>.Windows.NewLine.ToArray());

        Assert.Throws<NotSupportedException>(() => CsvParserOptions<int>.Environment);
        Assert.Throws<NotSupportedException>(() => CsvParserOptions<int>.Windows);
        Assert.Throws<NotSupportedException>(() => CsvParserOptions<int>.Unix);
    }

    [Fact]
    public static void Should_Implement_IEquatable()
    {
        var o1 = new CsvParserOptions<char>();
        var o2 = new CsvParserOptions<char>();
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
