using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static class CsvConverterFactoryTests
{
    [Fact]
    public static void Should_Validate_Returned()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var factory = new BorkedFactory { ReturnsNull = true };
            _ = factory.GetAsConverter(typeof(char), CsvOptions<char>.Default);
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            var factory = new BorkedFactory { ReturnsItself = true };
            _ = factory.GetAsConverter(typeof(char), CsvOptions<char>.Default);
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            var factory = new BorkedFactory { ReturnsItself = false };
            _ = factory.GetAsConverter(typeof(char), CsvOptions<char>.Default);
        });

        // valid example
        var validFactory = new ValidFactory();
        var converter = validFactory.GetAsConverter(typeof(string), CsvOptions<char>.Default);
        Assert.IsType<StringTextConverter>(converter);

        Assert.Same(converter, converter.GetAsConverter(typeof(string), CsvOptions<char>.Default));
    }
}

file sealed class ValidFactory : CsvConverterFactory<char>
{
    public override bool CanConvert(Type type) => type == typeof(string);

    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        Assert.Equal(typeof(string), type);
        return StringTextConverter.Instance;
    }
}

file sealed class BorkedFactory : CsvConverterFactory<char>
{
    public bool ReturnsNull { get; set; }
    public bool ReturnsItself { get; set; }

    public override bool CanConvert(Type type) => true;

    public override CsvConverter<char> Create(Type type, CsvOptions<char> options)
    {
        if (ReturnsNull)
        {
            return null!;
        }

        return ReturnsItself ? this : StringTextConverter.Instance;
    }
}
