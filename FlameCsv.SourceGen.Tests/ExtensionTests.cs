using System.Diagnostics.CodeAnalysis;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Tests;

public static class ExtensionTests
{
    [Theory]
    [InlineData(null, "default")]
    [InlineData(1, "1")]
    [InlineData("abc", "\"abc\"")]
    [InlineData('a', "'a'")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public static void Test_ToLiteral(object? value, string expected)
    {
        Assert.Equal(expected, value.ToLiteral());
    }

    [Theory]
    [InlineData(null, "null")]
    [InlineData("", "\"\"")]
    [InlineData("abc", "\"abc\"")]
    [InlineData("test\\a", "\"test\\\\a\"")]
    public static void Test_ToStringLiteral(string? value, string expected)
    {
        Assert.Equal(expected, value.ToStringLiteral());
    }

    [Fact]
    [SuppressMessage("ReSharper", "UseCollectionExpression")]
    public static void Test_EquatableArray()
    {
        Assert.Equal(EquatableArray<int>.Empty, EquatableArray<int>.Empty);
        Assert.Equal(EquatableArray<int>.Empty, new EquatableArray<int>());
        Assert.Equal(EquatableArray<int>.Empty, []);
        Assert.Equal(EquatableArray<int>.Empty, new EquatableArray<int>(Array.Empty<int>()));

        Assert.NotEqual(EquatableArray<int>.Empty, EquatableArray.Create([1]));
        Assert.Equal(new EquatableArray<int>([1]), new EquatableArray<int>([1]));
        Assert.NotEqual(new EquatableArray<int>([1]), new EquatableArray<int>([2]));

        Assert.Equal(new EquatableArray<int>([1, 2]), new EquatableArray<int>([1, 2]));
        Assert.NotEqual(new EquatableArray<int>([1, 2]), new EquatableArray<int>([1, 3]));

        Assert.Equal(
            new EquatableArray<(string name, string display)>([("x", "y"), ("a", "b")]),
            new EquatableArray<(string name, string display)>([("x", "y"), ("a", "b")]));

        // ReSharper disable once RedundantCast
        Assert.Equal(EquatableArray.Create(1, 2, 3), new EquatableArray<int>((ReadOnlySpan<int>) [1, 2, 3]));
    }
}
