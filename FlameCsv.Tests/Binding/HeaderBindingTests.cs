using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Tests.Utilities;

// ReSharper disable all

namespace FlameCsv.Tests.Binding;

public static class HeaderBindingTests
{
    [Fact]
    public static void Should_Bind_To_Ctor_Parameter()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { Comparer = StringComparer.Ordinal }, false);
        var materializer = binder.GetMaterializer<ShimWithCtor>(["Name", "_targeted"]);
        var record = new ConstantRecord<char>(["Test", "true"]);
        var result = materializer.Parse(ref record);

        Assert.True(result.IsEnabled);
        Assert.Equal("Test", result.Name);

        // should cache
        Assert.Same(materializer, binder.GetMaterializer<ShimWithCtor>(["Name", "_targeted"]));
    }

    [Fact]
    public static void Should_Bind_To_Properties()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { Comparer = StringComparer.Ordinal }, false);

        var materializer = binder.GetMaterializer<Shim>(["IsEnabled", "Name", "_targeted"]);

        // should require exactly 3 fields
        var record = new ConstantRecord<char>(["true", "Test", "1"]);
        var result = materializer.Parse(ref record);

        Assert.True(result.IsEnabled);
        Assert.Equal("Test", result.DisplayName);
        Assert.Equal(1, result.Targeted);

        // should cache
        Assert.Same(materializer, binder.GetMaterializer<Shim>(["IsEnabled", "Name", "_targeted"]));
    }

    [Fact]
    public static void Should_Include_Name_In_Exception_Message()
    {
        var ex = Record.Exception(
            () => CsvReader.Read<Shim>("IsEnabled,Name,_targeted\r\ntrue,name,\0\r\n").ToList());

        Assert.IsType<CsvUnhandledException>(ex);
        Assert.IsType<CsvParseException>(ex.InnerException);
        Assert.Contains("Targeted", ex.InnerException?.Message, StringComparison.Ordinal);
    }
}

[CsvHeaderTarget(nameof(Targeted), "_targeted")]
file class Shim
{
    [CsvHeaderExclude] public string? Name { get; set; }
    [CsvHeader("Name")] public string? DisplayName { get; set; }
    public bool IsEnabled { get; set; }
    public int Targeted { get; set; }
}

file class ShimWithCtor([CsvHeader("_targeted")] bool isEnabled)
{
    public object? _Targeted { get; set; }

    public string? Name { get; set; }
    public bool IsEnabled { get; } = isEnabled;
}
