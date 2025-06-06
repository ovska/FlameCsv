using FlameCsv.Attributes;
using FlameCsv.Binding;

[assembly: CsvIndex(0, MemberName = "Id", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped))]
[assembly: CsvIndex(1, MemberName = "Name", TargetType = typeof(FlameCsv.Tests.Binding.AssemblyScoped))]

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
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Members>)b).Member.Name))
        );
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Targets(bool write)
    {
        Assert.True(IndexAttributeBinder<Class>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal(
            [(0, "A"), (1, "B"), (2, "C")],
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Class>)b).Member.Name))
        );
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Mixed(bool write)
    {
        Assert.True(IndexAttributeBinder<Mixed>.TryGetBindings(write, out var result));
        Assert.Equal(3, result.Bindings.Length);
        Assert.Equal(
            [(0, "A"), (1, "B"), (2, "C")],
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Mixed>)b).Member.Name))
        );
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
                result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Params>)b).Member.Name))
            );
        }
        else
        {
            Assert.Equal(
                [(0, "c"), (1, "b"), (2, "a")],
                result.Bindings.ToArray().Select(b => (b.Index, ((ParameterCsvBinding<Params>)b).Parameter.Name!))
            );
        }
    }

    [Theory(Skip = "Type proxy api not yet finalized"), InlineData(true), InlineData(false)]
    public static void Should_Handle_Interfaces(bool write)
    {
        Assert.True(IndexAttributeBinder<IFace>.TryGetBindings(write, out var result));
        Assert.Equal(2, result.Bindings.Length);
        Assert.Equal("A", ((MemberCsvBinding<IFace>)result.Bindings[0]).Member.Name);
        Assert.Equal("B", ((MemberCsvBinding<IFace>)result.Bindings[1]).Member.Name);
    }

    [Fact]
    public static void Should_Ignore_Duplicates()
    {
        Assert.True(IndexAttributeBinder<Duplicates>.TryGetBindings(true, out var result));
        Assert.Equal(4, result.Bindings.Length);
        Assert.Equal("A", ((MemberCsvBinding<Duplicates>)result.Bindings[0]).Member.Name);
        Assert.Equal("B", ((MemberCsvBinding<Duplicates>)result.Bindings[1]).Member.Name);
        Assert.Equal(new IgnoredCsvBinding<Duplicates>(2), result.Bindings[2]);
        Assert.Equal("C", ((MemberCsvBinding<Duplicates>)result.Bindings[3]).Member.Name);
    }


    private class ObjIFace : IFace
    {
        public int A { get; set; }
        public string? B { get; set; }
    }

    [CsvTypeProxy(typeof(ObjIFace))]
    private interface IFace
    {
        [CsvIndex(0)]
        int A { get; }

        [CsvIndex(1)]
        string? B { get; }
    }

    [CsvIgnoredIndexes(1)]
    private class Ignored
    {
        [CsvIndex(0)]
        public int A { get; set; }

        [CsvIndex(2)]
        public int B { get; set; }
    }

    private class Members
    {
        [CsvIndex(0)]
        public int A { get; set; }

        [CsvIndex(1)]
        public string? B { get; set; }

        [CsvIndex(2)]
        public bool C { get; set; }
    }

    [CsvIndex(0, MemberName = nameof(A))]
    [CsvIndex(1, MemberName = nameof(B))]
    [CsvIndex(2, MemberName = nameof(C))]
    private class Class
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }

    [CsvIndex(1, MemberName = nameof(B))]
    private class Mixed
    {
        [CsvIndex(0)]
        public int A { get; set; }
        public string? B { get; set; }

        [CsvIndex(2)]
        public bool C { get; set; }
    }

    private class Params([CsvIndex(2)] int a, [CsvIndex(1)] string? b, [CsvIndex(0)] bool c)
    {
        [CsvIndex(0)]
        public int A { get; } = a;

        [CsvIndex(1)]
        public string? B { get; } = b;

        [CsvIndex(2)]
        public bool C { get; } = c;
    }

    private class None
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }

    [CsvIgnoredIndexes(2)]
    private class Duplicates
    {
        [CsvIndex(0)]
        [CsvIndex(0)]
        public int A { get; set; }

        [CsvIndex(1)]
        public string? B { get; set; }

        [CsvIndex(3)]
        [CsvIndex(3)]
        public bool C { get; set; }
    }
}
