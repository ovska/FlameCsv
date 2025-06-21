using System.Reflection;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Binding;

public static class StringPoolAttributeConverterTests
{
    [Fact]
    public static void Should_Return_Converter_For_Char()
    {
        var props = typeof(Shim)
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetCustomAttribute<CsvStringPoolingAttribute>()!);

        Assert.True(
            props[nameof(Shim.Name)].TryCreateConverter(typeof(string), CsvOptions<char>.Default, out var converter)
        );
        Assert.IsType<CsvPoolingStringTextConverter>(converter);
        Assert.Same(StringPool.Shared, ((CsvPoolingStringTextConverter)converter).Pool);
    }

    [Fact]
    public static void Should_Return_Converter_For_Byte()
    {
        var props = typeof(Shim)
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetCustomAttribute<CsvStringPoolingAttribute>()!);

        Assert.True(
            props[nameof(Shim.Name)].TryCreateConverter(typeof(string), CsvOptions<byte>.Default, out var converter)
        );
        Assert.IsType<CsvPoolingStringUtf8Converter>(converter);
        Assert.Same(StringPool.Shared, ((CsvPoolingStringUtf8Converter)converter).Pool);
    }

    private class Shim
    {
        [CsvStringPooling]
        public string? Name { get; set; }
    }

    private class Provider
    {
        public static StringPool Value { get; } = new();
    }
}
