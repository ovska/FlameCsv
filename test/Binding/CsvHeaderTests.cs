namespace FlameCsv.Tests.Binding;

public static class CsvHeaderTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Test1")]
    [InlineData("Test2")]
    [InlineData("very_long_header_name")]
    [InlineData("another_very_long_header_name_that_is_longer_than_the_provided_buffer")]
    public static void Should_Cache_Headers(string value)
    {
        Impl<byte>();
        Impl<char>();

        void Impl<T>()
            where T : unmanaged, IBinaryInteger<T>
        {
            CsvHeader.HeaderPool?.Reset();
            var buffer = new char[32];
            var tokens = CsvOptions<T>.GetFromString(value).Span;
            var header1 = CsvHeader.Get(tokens, buffer);
            var header2 = CsvHeader.Get(tokens, buffer);

            Assert.Equal(value, header1);
            Assert.Equal(value, header2);

            if (CsvOptions<T>.GetAsString(tokens).Length <= buffer.Length)
            {
                Assert.Same(header1, header2);
            }
            else
            {
                Assert.NotSame(header1, header2);
            }
        }
    }

    [Fact]
    public static void Should_Return_By_Name()
    {
        var header = new CsvHeader(StringComparer.Ordinal, ["A", "B", "C"]);

        Assert.True(header.TryGetValue("A", out int index));
        Assert.Equal(0, index);
        Assert.True(header.TryGetValue("B", out index));
        Assert.Equal(1, index);
        Assert.True(header.TryGetValue("C", out index));
        Assert.Equal(2, index);
        Assert.False(header.TryGetValue("D", out index));

        Assert.True(header.ContainsKey("A"));
        Assert.True(header.ContainsKey("B"));
        Assert.True(header.ContainsKey("C"));
        Assert.False(header.ContainsKey("D"));

        Assert.Throws<ArgumentNullException>(() => header.TryGetValue(null!, out _));
        Assert.Throws<ArgumentNullException>(() => header.ContainsKey(null!));
    }

    [Fact]
    public static void Should_Equal()
    {
        var header = new CsvHeader(StringComparer.Ordinal, ["A", "B", "C"]);
        var header2 = new CsvHeader(StringComparer.Ordinal, ["A", "B", "C"]);

        Assert.Equal(header, header2);
        Assert.True(header.Equals(header2));
        Assert.True(header.Equals((object)header2));
        Assert.True(header.GetHashCode() == header2.GetHashCode());

        var header3 = new CsvHeader(StringComparer.OrdinalIgnoreCase, ["A", "B", "C"]);
        Assert.NotEqual(header, header3);
        Assert.False(header.Equals(header3));
        Assert.False(header.Equals((object)header3));
        Assert.False(header.GetHashCode() == header3.GetHashCode());

        // for code coverage
        Assert.NotNull(header.ToString());
        Assert.True(header.Equals(header));
        Assert.False(header.Equals(null));
    }
}
