using FlameCsv.Binding.Providers;

// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace FlameCsv.Tests.Binding;

public static class ManualBindingTests
{
    [Fact]
    public static void Should_Bind_To_Members()
    {
        var provider = new ManualBindingProvider<char, Target>()
            .Add(0, "A")
            .Add(1, typeof(Target).GetProperty("B")!)
            .Add(2, t => t.C);

        Assert.True(provider.TryGetBindings<Target>(out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.Select(b => (b.Index, b.Member.Name)));
    }

    [Fact]
    public static void Should_Bind_To_Named_Field()
    {
        var provider = new ManualBindingProvider<char, Target>().Add(0, "Field");
        Assert.True(provider.TryGetBindings<Target>(out var result));
        Assert.Single(result!.Bindings);
        Assert.Equal("Field", result.Bindings[0].Member.Name);
    }

    [Fact]
    public static void Should_Handle_No_Bindings()
    {
        var provider = new ManualBindingProvider<char, Target>();
        Assert.False(provider.TryGetBindings<Target>(out _));
    }

    [Fact]
    public static void Should_Throw_On_Invalid_Type()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ManualBindingProvider<char, Target>().TryGetBindings<object>(out _));
    }

    [Fact]
    public static void Should_Throw_On_Invalid_Member_Name()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ManualBindingProvider<char, Target>().Add(0, "xyz"));
    }
    
    private class Target
    {
        public int A { get; set; }
        public string? B { get; set; }
        public bool C { get; set; }
#pragma warning disable CS0649
        public Guid Field;
#pragma warning restore CS0649
    }
}
