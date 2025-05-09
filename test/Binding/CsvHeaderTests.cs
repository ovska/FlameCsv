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
            var tokens = CsvOptions<T>.Default.GetFromString(value).Span;
            var header1 = CsvHeader.Get(CsvOptions<T>.Default, tokens, buffer);
            var header2 = CsvHeader.Get(CsvOptions<T>.Default, tokens, buffer);

            Assert.Equal(value, header1);
            Assert.Equal(value, header2);

            if (CsvOptions<T>.Default.GetAsString(tokens).Length <= buffer.Length)
            {
                Assert.Same(header1, header2);
            }
            else
            {
                Assert.NotSame(header1, header2);
            }
        }
    }
}
