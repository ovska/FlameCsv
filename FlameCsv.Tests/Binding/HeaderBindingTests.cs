using System.Buffers;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Reading;

// ReSharper disable UnusedType.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember

namespace FlameCsv.Tests.Binding;

public static class HeaderBindingTests
{
    [CsvHeaderTarget(nameof(Targeted), "_targeted")]
    private class Shim
    {
        [CsvHeaderExclude] public string? Name { get; set; }
        [CsvHeader("Name")] public string? DisplayName { get; set; }
        public bool IsEnabled { get; set; }
        public int Targeted { get; set; }
    }

    private class ShimWithCtor
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Intended")]
        public object? _Targeted { get; }

        public string? Name { get; set; }
        public bool IsEnabled { get; }

        public ShimWithCtor(
            [CsvHeader("_targeted")] bool isEnabled)
        {
            IsEnabled = isEnabled;
        }
    }

    [Fact]
    public static void Should_Bind_To_Ctor_Parameter()
    {
        var binder = new DefaultHeaderBinder<char>(new CsvTextReaderOptions { Comparison = StringComparison.Ordinal });
        var bindingCollection = binder.Bind<ShimWithCtor>(new[] { "Name", "_targeted" });
        var byIndex = bindingCollection.Bindings.ToArray().ToDictionary(b => b.Index);
        Assert.Equal(2, byIndex.Count);
        Assert.Equal("Name", ((MemberCsvBinding<ShimWithCtor>)byIndex[0]).Member.Name);
        Assert.Equal("isEnabled", ((ParameterCsvBinding<ShimWithCtor>)byIndex[1]).Parameter.Name);
    }

    [Fact]
    public static void Should_Bind_To_Properties()
    {
        var binder = new DefaultHeaderBinder<char>(new CsvTextReaderOptions { Comparison = StringComparison.Ordinal });

        var bindingCollection = binder.Bind<Shim>(new[] { "IsEnabled", "Name", "_targeted" });
        Assert.Equal(3, bindingCollection.Bindings.Length);
        var byIndex = bindingCollection.MemberBindings.ToArray().ToDictionary(b => b.Index, b => b.Member);
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

        var options = new CsvTextReaderOptions { Comparison = StringComparison.Ordinal };

        using var processor = new CsvHeaderProcessor<char, Shim>(options);
        var buffer = new ReadOnlySequence<char>(data.AsMemory());

        Assert.True(processor.TryRead(ref buffer, out var value1, false));
        Assert.True(processor.TryRead(ref buffer, out var value2, false));
        Assert.False(processor.TryRead(ref buffer, out _, false));
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

        var options = new CsvTextReaderOptions { Comparison = StringComparison.Ordinal };

        using var processor = new CsvHeaderProcessor<char, Shim>(options);
        var buffer = new ReadOnlySequence<char>(data.AsMemory());
        Assert.False(processor.TryRead(ref buffer, out _, false));
        Assert.False(processor.TryRead(ref buffer, out _, true));
    }
}
