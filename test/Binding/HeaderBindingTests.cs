using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Exceptions;

[assembly: CsvHeader("_id", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped), MemberName = "Id")]
[assembly: CsvHeader("_name", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped), MemberName = "Name")]

namespace FlameCsv.Tests.Binding;

file sealed class AssemblyScoped
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public static partial class HeaderBindingTests
{
    [Fact]
    public static void Should_Bind_Using_Assembly_Attributes()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { IgnoreHeaderCase = false });
        var materializer = binder.GetMaterializer<AssemblyScoped>(["_id", "_name"]);

        using (ConstantRecord.Create(out var record, "5", "Test"))
        {
            var result = materializer.Parse(record);
            Assert.Equal(5, result.Id);
            Assert.Equal("Test", result.Name);
        }
    }

    [Fact]
    public static void Should_Bind_To_Ctor_Parameter()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { IgnoreHeaderCase = false });
        var materializer = binder.GetMaterializer<ShimWithCtor>(["Name", "_targeted"]);

        using (ConstantRecord.Create(out var record, "Test", "true"))
        {
            var result = materializer.Parse(record);

            Assert.True(result.IsEnabled);
            Assert.Equal("Test", result.Name);

            // should cache
            Assert.Same(materializer, binder.GetMaterializer<ShimWithCtor>(["Name", "_targeted"]));
        }
    }

    [Fact]
    public static void Should_Bind_To_Properties()
    {
        var binder = new CsvReflectionBinder<char>(new CsvOptions<char> { IgnoreHeaderCase = false });

        var materializer = binder.GetMaterializer<Shim>(["IsEnabled", "Name", "_targeted"]);

        // should require exactly 3 fields

        using (ConstantRecord.Create(out var record, "true", "Test", "1"))
        {
            var result = materializer.Parse(record);

            Assert.True(result.IsEnabled);
            Assert.Equal("Test", result.DisplayName);
            Assert.Equal(1, result.Targeted);

            // should cache
            Assert.Same(materializer, binder.GetMaterializer<Shim>(["IsEnabled", "Name", "_targeted"]));
        }
    }

    [Fact]
    public static void Should_Include_Name_In_Exception_Message()
    {
        const string data = "IsEnabled,Name,_targeted\r\ntrue,name,\0\r\n";
        var ex = Record.Exception(() => Csv.From(data).Read<Shim>().ToList()) as CsvParseException;

        Assert.NotNull(ex);
        Assert.Contains("Targeted", ex.Message, StringComparison.Ordinal);
        Assert.Equal("_targeted", ex.HeaderValue);
        Assert.Equal(2, ex.FieldIndex);
        Assert.Equal(typeof(int), ex.TargetType);
        Assert.Equal(2, ex.Line);
        Assert.Equal("IsEnabled,Name,_targeted\r\n".Length, ex.RecordPosition);
        Assert.Equal("IsEnabled,Name,_targeted\r\ntrue,name,".Length, ex.FieldPosition);
    }

    [Fact]
    public static void Should_Use_Type_Proxy()
    {
        var materializer = CsvOptions<char>.Default.TypeBinder.GetMaterializer<ISomething>(
            ["IsEnabled", "Name", "Targeted"]
        );

        using (ConstantRecord.Create(out var record, "true", "Test", "1"))
        {
            ISomething result = materializer.Parse(record);

            Assert.IsType<Something>(result);
            Assert.True(result.IsEnabled);
            Assert.Equal("Test", result.Name);
            Assert.Equal(1, result.Targeted);
        }
    }

    [Theory(Skip = "TODO"), InlineData(true), InlineData(false)]
    public static void Should_Not_Use_Write_Configuration_From_Proxy(bool sourceGen)
    {
        _ = sourceGen;
        var sb = new System.Text.StringBuilder();
        Csv.To(new StringWriter(sb)).Write<ISomething>([]);
        Assert.Equal("_name,_isenabled,_targeted\r\n", sb.ToString());
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Use_Read_Configuration_From_Underlying(bool sourceGen)
    {
        _ = sourceGen;

        var opts = CsvOptions<char>.Default;
        var bindings = opts.TypeBinder.GetMaterializer<ISomething>(["Name", "IsEnabled", "Targeted"]);

        using (ConstantRecord.Create(out var record, "Test", "true", "1"))
        {
            ISomething obj = bindings.Parse(record);

            Assert.IsType<Something>(obj);
            Assert.True(obj.IsEnabled);
            Assert.Equal("Test", obj.Name);
            Assert.Equal(1, obj.Targeted);
        }
    }

    // [CsvTypeMap<char, ISomething>]
    // private partial class ProxyTypeMap;

    [CsvTypeProxy(typeof(Something))]
    private interface ISomething
    {
        [CsvHeader("_name")]
        string? Name { get; }

        [CsvHeader("_isenabled")]
        bool IsEnabled { get; }

        [CsvHeader("_targeted")]
        int Targeted { get; }
    }

    private class Something : ISomething
    {
        public string? Name { get; set; }
        public bool IsEnabled { get; set; }
        public int Targeted { get; set; }
    }
}

[CsvHeader("_targeted", MemberName = nameof(Targeted))]
file class Shim
{
    [CsvIgnore]
    public string? Name { get; set; }

    [CsvHeader("Name")]
    public string? DisplayName { get; set; }
    public bool IsEnabled { get; set; }
    public int Targeted { get; set; }
}

file class ShimWithCtor([CsvHeader("_targeted")] bool isEnabled)
{
    public object? _Targeted { get; set; }

    public string? Name { get; set; }
    public bool IsEnabled { get; } = isEnabled;
}
