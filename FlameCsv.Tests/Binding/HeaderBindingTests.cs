using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;

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

    private class ShimWithCtor([CsvHeader("_targeted")] bool isEnabled)
    {
        // ReSharper disable once InconsistentNaming
        public object? _Targeted { get; }

        public string? Name { get; set; }
        public bool IsEnabled { get; } = isEnabled;
    }

    [Fact]
    public static void Should_Bind_To_Ctor_Parameter()
    {
        var binder = new DefaultHeaderBinder<char>(new CsvOptions<char> { Comparer = StringComparer.Ordinal });
        var bindingCollection = binder.Bind<ShimWithCtor>(["Name", "_targeted"]);
        var byIndex = bindingCollection.Bindings.ToArray().ToDictionary(b => b.Index);
        Assert.Equal(2, byIndex.Count);
        Assert.Equal("Name", ((MemberCsvBinding<ShimWithCtor>)byIndex[0]).Member.Name);
        Assert.Equal("isEnabled", ((ParameterCsvBinding<ShimWithCtor>)byIndex[1]).Parameter.Name);
    }

    [Fact]
    public static void Should_Bind_To_Properties()
    {
        var binder = new DefaultHeaderBinder<char>(new CsvOptions<char> { Comparer = StringComparer.Ordinal });

        var bindingCollection = binder.Bind<Shim>(["IsEnabled", "Name", "_targeted"]);
        Assert.Equal(3, bindingCollection.Bindings.Length);
        var byIndex = bindingCollection.MemberBindings.ToArray().ToDictionary(b => b.Index, b => b.Member);
        Assert.Equal(3, byIndex.Count);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.IsEnabled)), byIndex[0]);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.DisplayName)), byIndex[1]);
        Assert.Equal(typeof(Shim).GetProperty(nameof(Shim.Targeted)), byIndex[2]);
    }
}
