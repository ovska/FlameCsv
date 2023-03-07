using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

// ReSharper disable UnusedMember.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace FlameCsv.Tests.Binding;

public static class IndexAttributeBinderTests
{
    [Fact]
    public static void Should_Bind_To_Members()
    {
        Assert.True(IndexAttributeBinder.TryGet<Members>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result._bindingsSorted.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Bind_To_Targets()
    {
        Assert.True(IndexAttributeBinder.TryGet<Class>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result._bindingsSorted.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Bind_To_Mixed()
    {
        Assert.True(IndexAttributeBinder.TryGet<Mixed>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result._bindingsSorted.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Handle_No_Bindings()
    {
        Assert.False(IndexAttributeBinder.TryGet<None>(out _));
    }

    [Fact]
    public static void Should_Handle_Ignores()
    {
        Assert.True(IndexAttributeBinder.TryGet<Ignored>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal("A", result.Bindings[0].Member.Name);
        Assert.True(result.Bindings[1].IsIgnored);
        Assert.Equal("B", result.Bindings[2].Member.Name);
    }

    [CsvIndexIgnore(1)]
    private class Ignored
    {
        [CsvIndex(0)] public int A { get; set; }
        [CsvIndex(2)] public int B { get; set; }
    }

    private class Members
    {
        [CsvIndex(0)] public int A { get; set; }
        [CsvIndex(1)] public string? B { get; set; }
        [CsvIndex(2)] public bool C { get; set; }
    }

    [CsvIndexTarget(0, nameof(A))]
    [CsvIndexTarget(1, nameof(B))]
    [CsvIndexTarget(2, nameof(C))]
    private class Class
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }

    [CsvIndexTarget(1, nameof(B))]
    private class Mixed
    {
        [CsvIndex(0)] public int A { get; set; }
        public string? B { get; set; }
        [CsvIndex(2)] public bool C { get; set; }
    }

    private class None
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }
}
