using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static class StringPoolConverterTests
{
    [Fact]
    public static void Should_Pool()
    {
        StringPool pool = new(minimumSize: 32);
        var converter = new PoolingStringUtf8Converter(pool);

        var longString = new string('a', 1024);
        Assert.True(converter.TryParse(Encoding.UTF8.GetBytes(longString), out var value));
        Assert.Equal(longString, value);
        Assert.Same(value, pool.GetOrAdd(longString.AsSpan()));

        Assert.True(converter.TryParse("Hello, world!"u8, out value));
        Assert.Equal("Hello, world!", value);
        Assert.Same(value, pool.GetOrAdd("Hello, world!".AsSpan()));

        Assert.True(converter.TryParse([], out value));
        Assert.Equal("", value);
    }
}
