using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Utilities.Comparers;

namespace FlameCsv.Tests.Extensions;

public static class UtilityExtensionTests
{
    [Fact]
    public static void Should_Print()
    {
        string input = "\0\r\n\t\f\v\\abc\u0010";
        var actual = input.AsSpan().AsPrintableString();
        string expected = @"\0\r\n\t\f\v\\abc\u0010";
        Assert.Equal(expected, actual);
        Assert.Equal(expected, Encoding.UTF8.GetBytes(input).AsSpan().AsPrintableString());
        Assert.Equal("8182838D", ((ReadOnlySpan<byte>)([129, 130, 131, 141])).AsPrintableString());
        Assert.Equal("<empty>", Span<char>.Empty.AsPrintableString());
        Assert.Equal("<empty>", Span<byte>.Empty.AsPrintableString());
    }

    [Fact]
    public static void Should_Have_Nullable_Equality()
    {
        var comparer = NullableTypeEqualityComparer.Instance;

        Assert.Equal(default!, default(Type)!, comparer);
        Assert.Equal(typeof(int), typeof(int), comparer);
        Assert.Equal(typeof(int?), typeof(int?), comparer);
        Assert.Equal(typeof(int), typeof(int?), comparer);
        Assert.Equal(typeof(int?), typeof(int), comparer);
        Assert.NotEqual(default!, typeof(string), comparer);
        Assert.NotEqual(typeof(string), default!, comparer);
        Assert.Equal(typeof(string), typeof(string), comparer);

        var c = new CsvOptions<char> { NullTokens = { [typeof(int)] = "test" } };
        Assert.Contains(typeof(int?), c.NullTokens);
        Assert.Equal("test", c.NullTokens[typeof(int?)]);
        c.NullTokens.Clear();
        c.NullTokens[typeof(int?)] = "test";
        Assert.Contains(typeof(int), c.NullTokens);
        Assert.Equal("test", c.NullTokens[typeof(int)]);
    }

    [Fact]
    public static void Should_Create_Instance()
    {
        var type = typeof(TestObj);
        var instance = type.CreateInstance<TestObj>(42);
        Assert.NotNull(instance);
        Assert.Equal(42, instance.Value);

        Assert.Throws<InvalidOperationException>(() => type.CreateInstance<TestObj>(42, (object?)null));
    }
}

file class TestObj(int i)
{
    public int Value => i;
}
