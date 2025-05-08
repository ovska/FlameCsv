using System.Reflection;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static class StringPoolAttributeConverterTests
{
    [Fact]
    public static void Should_Return_Converter()
    {
        var attrs = typeof(Shim).GetProperties().ToDictionary(
            p => p.Name,
            p => p.GetCustomAttribute<CsvStringPoolingAttribute>()!);

        Assert.True(attrs[nameof(Shim.Name)].TryCreateConverter<char>(typeof(string), CsvOptions<char>.Default, out var converter));
        Assert.IsType<PoolingStringTextConverter>(converter);
        Assert.Same(StringPool.Shared, ((PoolingStringTextConverter)converter).Pool);

        Assert.True(attrs[nameof(Shim.Description)].TryCreateConverter<char>(typeof(string), CsvOptions<char>.Default, out converter));
        Assert.IsType<PoolingStringTextConverter>(converter);
        Assert.Same(Provider.Value, ((PoolingStringTextConverter)converter).Pool);
    }

    private class Shim
    {
        [CsvStringPooling]
        public string? Name { get; set; }

        [CsvStringPooling(ProviderType = typeof(Provider), MemberName = "Value")]
        public string? Description { get; set; }
    }

    private class Provider
    {
        public static StringPool Value { get; } = new();
    }
}
