using FlameCsv.Utilities.Comparers;

// ReSharper disable PreferConcreteValueOverDefault

namespace FlameCsv.Tests.Extensions;

public static class UtilityExtensionTests
{
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
}
