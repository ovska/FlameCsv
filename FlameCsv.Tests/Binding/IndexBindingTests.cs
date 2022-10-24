using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Providers;

// ReSharper disable UnusedMember.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace FlameCsv.Tests.Binding;

public static class IndexBindingTests
{
    [Fact]
    public static void Should_Bind_To_Members()
    {
        var provider = new IndexBindingProvider<char>();
        Assert.True(provider.TryGetBindings<Members>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Bind_To_Targets()
    {
        var provider = new IndexBindingProvider<char>();
        Assert.True(provider.TryGetBindings<Class>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Bind_To_Mixed()
    {
        var provider = new IndexBindingProvider<char>();
        Assert.True(provider.TryGetBindings<Mixed>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Handle_No_Bindings()
    {
        var provider = new IndexBindingProvider<char>();
        Assert.False(provider.TryGetBindings<None>(out _));
    }

    [Fact]
    public static void Should_Handle_Ignores()
    {
        var provider = new IndexBindingProvider<char>();
        Assert.True(provider.TryGetBindings<Ignored>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal("A", result.Bindings[0].Member.Name);
        Assert.True(result.Bindings[1].IsIgnored);
        Assert.Equal("B", result.Bindings[2].Member.Name);
    }

    [IndexBindingIgnore(1)]
    private class Ignored
    {
        [IndexBinding(0)] public int A { get; set; }
        [IndexBinding(2)] public int B { get; set; }
    }

    private class Members
    {
        [IndexBinding(0)] public int A { get; set; }
        [IndexBinding(1)] public string? B { get; set; }
        [IndexBinding(2)] public bool C { get; set; }
    }

    [IndexBindingTarget(0, nameof(A))]
    [IndexBindingTarget(1, nameof(B))]
    [IndexBindingTarget(2, nameof(C))]
    private class Class
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }

    [IndexBindingTarget(1, nameof(B))]
    private class Mixed
    {
        [IndexBinding(0)] public int A { get; set; }
        public string? B { get; set; }
        [IndexBinding(2)] public bool C { get; set; }
    }

    private class None
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
    }
}
