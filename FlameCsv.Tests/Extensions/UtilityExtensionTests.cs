using System.Runtime.InteropServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Extensions;

public static class UtilityExtensionTests
{
    [Fact]
    public static void Should_Have_Nullable_Equality()
    {
        var comparer = NullableTypeEqualityComparer.Instance;

        Assert.Equal(default(Type)!, default(Type)!, comparer);
        Assert.Equal(typeof(int), typeof(int), comparer);
        Assert.Equal(typeof(int?), typeof(int?), comparer);
        Assert.Equal(typeof(int), typeof(int?), comparer);
        Assert.Equal(typeof(int?), typeof(int), comparer);
        Assert.NotEqual(default(Type)!, typeof(string), comparer);
        Assert.NotEqual(typeof(string), default(Type)!, comparer);
        Assert.Equal(typeof(string), typeof(string), comparer);

        var c = new CsvOptions<char>();
        c.NullTokens[typeof(int)] = "test";
        Assert.Contains(typeof(int?), c.NullTokens);
        Assert.Equal("test", c.NullTokens[typeof(int?)]);
        c.NullTokens.Clear();
        c.NullTokens[typeof(int?)] = "test";
        Assert.Contains(typeof(int), c.NullTokens);
        Assert.Equal("test", c.NullTokens[typeof(int)]);
    }

    [Theory]
    [InlineData(true, CsvBindingScope.All, true)]
    [InlineData(false, CsvBindingScope.All, true)]
    [InlineData(true, CsvBindingScope.Read, false)]
    [InlineData(false, CsvBindingScope.Read, true)]
    [InlineData(true, CsvBindingScope. Write, true)]
    [InlineData(false, CsvBindingScope.Write, false)]
    public static void Should_Return_Valid_Scope(bool write, CsvBindingScope scope, bool valid)
    {
        Assert.Equal(valid, scope.IsValidFor(write));
    }

    [Fact]
    public static void Should_Make_Copy_Of_Data()
    {
        var first = new int[] { 0, 1, 2 };

        var mem = new ReadOnlyMemory<int>(first);
        var copy = mem.SafeCopy();

        first[0] = 2;

        Assert.NotEqual(first, copy.ToArray());
    }

    [Fact]
    public static void Should_Reuse_String_Memory_Instances()
    {
        var first = "Test".AsMemory();
        var second = first.SafeCopy();

        Assert.True(MemoryMarshal.TryGetString(first, out string? s1, out _, out _));
        Assert.True(MemoryMarshal.TryGetString(second, out string? s2, out _, out _));
        Assert.Same(s1, s2);
    }
}
