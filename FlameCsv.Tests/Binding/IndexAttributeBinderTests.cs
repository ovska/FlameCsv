using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;

[assembly: CsvAssemblyType(typeof(FlameCsv.Tests.Binding.AssemblyScoped), IgnoredHeaders = ["xyz"])]
[assembly: CsvAssemblyTypeField(typeof(FlameCsv.Tests.Binding.AssemblyScoped), "Id", Index = 0)]
[assembly: CsvAssemblyTypeField(typeof(FlameCsv.Tests.Binding.AssemblyScoped), "Name", Index = 1)]

namespace FlameCsv.Tests.Binding;

file sealed class AssemblyScoped
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public static class IndexAttributeBinderTests
{
    [Fact]
    public static void Should_Bind_Using_Assembly_Attribute()
    {
        Assert.True(IndexAttributeBinder<AssemblyScoped>.TryGetBindings(true, out var result));
        Assert.Equal(2, result.Bindings.Length);
        Assert.Equal("Id", ((MemberCsvBinding<AssemblyScoped>)result.Bindings[0]).Member.Name);
        Assert.Equal("Name", ((MemberCsvBinding<AssemblyScoped>)result.Bindings[1]).Member.Name);
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Members(bool write)
    {
        Assert.True(IndexAttributeBinder<Members>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal(
            [(0, "A"), (1, "B"), (2, "C")],
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Members>)b).Member.Name)));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Targets(bool write)
    {
        Assert.True(IndexAttributeBinder<Class>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal(
            [(0, "A"), (1, "B"), (2, "C")],
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Class>)b).Member.Name)));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Mixed(bool write)
    {
        Assert.True(IndexAttributeBinder<Mixed>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal(
            [(0, "A"), (1, "B"), (2, "C")],
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Mixed>)b).Member.Name)));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Handle_No_Bindings(bool write)
    {
        Assert.False(IndexAttributeBinder<None>.TryGetBindings(write, out _));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Handle_Ignores(bool write)
    {
        Assert.True(IndexAttributeBinder<Ignored>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal("A", ((MemberCsvBinding<Ignored>)result.Bindings[0]).Member.Name);
        Assert.True(result.Bindings[1].IsIgnored);
        Assert.Equal("B", ((MemberCsvBinding<Ignored>)result.Bindings[2]).Member.Name);
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Ctor_Params(bool write)
    {
        Assert.True(IndexAttributeBinder<Params>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);

        if (write)
        {
            Assert.Equal(
                [(0, "A"), (1, "B"), (2, "C")],
                result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Params>)b).Member.Name)));
        }
        else
        {
            Assert.Equal(
                [(0, "c"), (1, "b"), (2, "a")],
                result.Bindings.ToArray().Select(b => (b.Index, ((ParameterCsvBinding<Params>)b).Parameter.Name!)));
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Handle_Interfaces(bool write)
    {
        Assert.True(IndexAttributeBinder<IFace>.TryGetBindings(write, out var result));
        Assert.Equal(2, result.Bindings.Length);
        Assert.Equal("A", ((MemberCsvBinding<IFace>)result.Bindings[0]).Member.Name);
        Assert.Equal("B", ((MemberCsvBinding<IFace>)result.Bindings[1]).Member.Name);
    }

    private class ObjIFace : IFace
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    [CsvType(CreatedTypeProxy = typeof(ObjIFace))]
    private interface IFace
    {
        [CsvField(Index = 0)] int A { get; }
        [CsvField(Index = 1)] string? B { get; }
    }

    [CsvType(IgnoredIndexes = [1])]
    private class Ignored
    {
        [CsvField(Index = 0)] public int A { get; set; }
        [CsvField(Index = 2)] public int B { get; set; }
    }

    private class Members
    {
        [CsvField(Index = 0)] public int A { get; set; }
        [CsvField(Index = 1)] public string? B { get; set; }
        [CsvField(Index = 2)] public bool C { get; set; }
    }

    [CsvTypeField(nameof(A), Index = 0)]
    [CsvTypeField(nameof(B), Index = 1)]
    [CsvTypeField(nameof(C), Index = 2)]
    private class Class
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }

    [CsvTypeField(nameof(B), Index = 1)]
    private class Mixed
    {
        [CsvField(Index = 0)] public int A { get; set; }
        public string? B { get; set; }
        [CsvField(Index = 2)] public bool C { get; set; }
    }

    private class Params([CsvField(Index = 2)] int a, [CsvField(Index = 1)] string? b, [CsvField(Index = 0)] bool c)
    {
        [CsvField(Index = 0)] public int A { get; } = a;
        [CsvField(Index = 1)] public string? B { get; } = b;
        [CsvField(Index = 2)] public bool C { get; } = c;
    }

    private class None
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }
}
