using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Tests.Utilities;

// ReSharper disable all

[assembly: CsvHeader("_id", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped), MemberName = "Id")]
[assembly: CsvHeader("_name", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped), MemberName = "Name")]

namespace FlameCsv.Tests.Binding;

file sealed class AssemblyScoped
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public static class HeaderBindingTests
{
    [Fact]
    public static void Should_Bind_Using_Assembly_Attributes()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { Comparer = StringComparer.Ordinal }, false);
        var materializer = binder.GetMaterializer<AssemblyScoped>(["_id", "_name"]);

        var record = new ConstantRecord<char>(["5", "Test"]);
        var result = materializer.Parse(ref record);

        Assert.Equal(5, result.Id);
        Assert.Equal("Test", result.Name);
    }

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
        var ex = Record.Exception(() => CsvReader.Read<Shim>("IsEnabled,Name,_targeted\r\ntrue,name,\0\r\n").ToList());

        Assert.IsType<CsvParseException>(ex);
        Assert.Contains("Targeted", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void Should_Use_Type_Proxy()
    {
        var materializer = CsvOptions<char>.Default.TypeBinder
            .GetMaterializer<ISomething>(["IsEnabled", "Name", "Targeted"]);

        var record = new ConstantRecord<char>(["true", "Test", "1"]);
        ISomething result = materializer.Parse(ref record);

        Assert.IsType<Something>(result);
        Assert.True(result.IsEnabled);
        Assert.Equal("Test", result.Name);
        Assert.Equal(1, result.Targeted);
    }
}

[CsvTypeProxy(typeof(Something))]
file interface ISomething
{
    string? Name { get; }
    bool IsEnabled { get; }
    int Targeted { get; }
}

file class Something : ISomething
{
    public string? Name { get; set; }
    public bool IsEnabled { get; set; }
    public int Targeted { get; set; }
}

[CsvHeader("_targeted", MemberName = nameof(Targeted))]
file class Shim
{
    [CsvIgnore] public string? Name { get; set; }
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
