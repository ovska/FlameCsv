using System.Buffers;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Readers;

// ReSharper disable UnusedType.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember

namespace FlameCsv.Tests.Binding;

public static class HeaderBindingTests
{
    [CsvHeaderTarget(nameof(Targeted), "_targeted")]
    private class Shim
    {
        [CsvHeaderIgnore] public string? Name { get; set; }
        [CsvHeader("Name")] public string? DisplayName { get; set; }
        public bool IsEnabled { get; set; }
        public int Targeted { get; set; }
    }

    [Theory]
    [InlineData("IsEnabled,Name,_targeted")]
    [InlineData("\"IsEnabled\",\"Name\",\"_targeted\"")]
    public static void Should_Bind_To_Properties(string header)
    {
        var binder = new HeaderTextBinder(stringComparison: StringComparison.Ordinal);

        var bindingCollection = binder.Bind<Shim>(header, CsvReaderOptions<char>.Default);
        var byIndex = bindingCollection.Bindings.ToDictionary(b => b.Index, b => b.Member);
        Assert.Equal(3, byIndex.Count);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.IsEnabled)), byIndex[0]);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.DisplayName)), byIndex[1]);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.Targeted)), byIndex[2]);
    }

    [Fact]
    public static void Should_Parse_Bound_Object()
    {
        const string data =
            "IsEnabled,Name,_targeted\r\n"
            + "true,Bob,1\r\n"
            + "false,Alice,2\r\n";

        var options = CsvReaderOptions<char>.Default;
        options.HeaderBinder = new HeaderTextBinder(stringComparison: StringComparison.Ordinal);

        using var processor = new CsvHeaderProcessor<char, Shim>(options);
        var buffer = new ReadOnlySequence<char>(data.AsMemory());

        Assert.True(processor.TryContinueRead(ref buffer, out var value1));
        Assert.True(processor.TryContinueRead(ref buffer, out var value2));
        Assert.False(processor.TryContinueRead(ref buffer, out _));
        Assert.True(buffer.IsEmpty);

        Assert.True(value1.IsEnabled);
        Assert.Equal("Bob", value1.DisplayName);
        Assert.Equal(1, value1.Targeted);

        Assert.False(value2.IsEnabled);
        Assert.Equal("Alice", value2.DisplayName);
        Assert.Equal(2, value2.Targeted);
    }

    [Fact]
    public static void Should_Bind_To_Only_Header()
    {
        const string data = "IsEnabled,Name,_targeted";

        var options = CsvReaderOptions<char>.Default;
        options.HeaderBinder = new HeaderTextBinder(stringComparison: StringComparison.Ordinal); 

        using var processor = new CsvHeaderProcessor<char, Shim>(options);
        var buffer = new ReadOnlySequence<char>(data.AsMemory());
        Assert.False(processor.TryContinueRead(ref buffer, out _));
        Assert.False(processor.TryReadRemaining(in buffer, out _));
    }
}
