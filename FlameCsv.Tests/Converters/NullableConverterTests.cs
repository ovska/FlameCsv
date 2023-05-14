using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static class NullableConverterTests
{
    [Fact]
    public static void Should_Return_Null()
    {
        var converter = new NullableConverter<char, int>(
            new Int32TextConverter(CsvTextOptions.Default),
            "".AsMemory());

        Assert.True(converter.TryParse("", out var value1));
        Assert.Null(value1);

        Assert.True(converter.TryParse("1", out var value2));
        Assert.Equal(1, value2);

        Assert.False(converter.TryParse(" ", out _));
    }

    [Fact]
    public static void Should_Create_Converter()
    {
        var factory = NullableConverterFactory<char>.Instance;

        Assert.True(factory.CanConvert(typeof(int?)));
        Assert.False(factory.CanConvert(typeof(int)));

        var emptyOptions = new CsvTextOptions
        {
            Converters = { factory, new Int32TextConverter(CsvTextOptions.Default) },
            Null = "null",
        };
        var parser = (CsvConverter<char, int?>)factory.Create(typeof(int?), emptyOptions);
        Assert.True(parser.TryParse("null", out var value));
        Assert.Null(value);
    }
}
