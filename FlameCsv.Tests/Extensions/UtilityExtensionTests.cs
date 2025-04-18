﻿using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

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

    [Fact]
    public static void Should_Make_Copy_Of_Data()
    {
        int[] first = [0, 1, 2];

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
