using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;

// ReSharper disable UnusedMember.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace FlameCsv.Tests.Binding;

public static class IndexAttributeBinderTests
{
    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Members(bool write)
    {
        Assert.True(IndexAttributeBinder<Members>.TryGetBindings(write, out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Members>)b).Member.Name)));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Targets(bool write)
    {
        Assert.True(IndexAttributeBinder<Class>.TryGetBindings(write, out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
            result.Bindings.ToArray().Select(b => (b.Index, ((MemberCsvBinding<Class>)b).Member.Name)));
    }

    [Theory, InlineData(true), InlineData(false)]
    public static void Should_Bind_To_Mixed(bool write)
    {
        Assert.True(IndexAttributeBinder<Mixed>.TryGetBindings(write, out var result));
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal(
            new[] { (0, "A"), (1, "B"), (2, "C") },
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
        Assert.Equal(3, result!.Bindings.Length);
        Assert.Equal("A", ((MemberCsvBinding<Ignored>)result.Bindings[0]).Member.Name);
        Assert.True(result.Bindings[1].IsIgnored);
        Assert.Equal("B", ((MemberCsvBinding<Ignored>)result.Bindings[2]).Member.Name);
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
