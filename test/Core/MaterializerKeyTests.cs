using System.Collections.Immutable;
using FlameCsv.Binding;

namespace FlameCsv.Tests;

public static class MaterializerKeyTests
{
    public static MatrixTheoryData<bool, string[]> ReadData { get; } =
        new(
            [true, false],
            [
                ["id", "name", "age"],
                ["id", "name", "age", "extra"],
                ["id", "name", "age", "extra1", "extra2"],
            ]
        );

    [Theory, MemberData(nameof(ReadData))]
    public static void Should_Be_Equatable_Read_Reflection(bool ignoreUnmatched, string[] headers)
    {
        MaterializerKey key = new(
            StringComparer.OrdinalIgnoreCase,
            typeof(object),
            ignoreUnmatched,
            headers.ToImmutableArray()
        );
        MaterializerKey key2 = new(
            StringComparer.OrdinalIgnoreCase,
            typeof(object),
            ignoreUnmatched,
            headers.ToImmutableArray()
        );
        Assert.Equal(key, key2);
        Assert.Equal(key.GetHashCode(), key2.GetHashCode());
        Assert.True(key.Equals((object)key2));

        MaterializerKey invalidKey = new(
            StringComparer.OrdinalIgnoreCase,
            typeof(object),
            !ignoreUnmatched,
            headers.ToImmutableArray()
        );
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());

        invalidKey = new(StringComparer.Ordinal, typeof(object), ignoreUnmatched, headers.ToImmutableArray());
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());

        invalidKey = new(StringComparer.OrdinalIgnoreCase, typeof(string), ignoreUnmatched, headers.ToImmutableArray());
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());

        invalidKey = new(
            StringComparer.OrdinalIgnoreCase,
            typeof(object),
            ignoreUnmatched,
            headers.ToImmutableArray().Add("extra")
        );
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());
    }

    [Theory, MemberData(nameof(ReadData))]
    public static void Should_Be_Equatable_Read_SourceGen(bool ignoreUnmatched, string[] headers)
    {
        var tm1 = new FakeTypeMap { IgnoreUnmatched = ignoreUnmatched };
        var tm2 = new AnotherFakeTypeMap { IgnoreUnmatched = ignoreUnmatched };
        MaterializerKey key = new(StringComparer.OrdinalIgnoreCase, tm1, headers.ToImmutableArray());
        MaterializerKey key2 = new(StringComparer.OrdinalIgnoreCase, tm1, headers.ToImmutableArray());
        Assert.Equal(key, key2);
        Assert.Equal(key.GetHashCode(), key2.GetHashCode());
        Assert.True(key.Equals((object)key2));

        MaterializerKey invalidKey = new(
            StringComparer.OrdinalIgnoreCase,
            new FakeTypeMap { IgnoreUnmatched = !ignoreUnmatched },
            headers.ToImmutableArray()
        );
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());

        invalidKey = new(StringComparer.Ordinal, tm1, headers.ToImmutableArray());
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());

        invalidKey = new(StringComparer.OrdinalIgnoreCase, tm1, headers.ToImmutableArray().Add("extra"));
        Assert.NotEqual(key, invalidKey);
        Assert.NotEqual(key.GetHashCode(), invalidKey.GetHashCode());
    }

    [Fact]
    public static void Should_Be_Equatable_Write_Reflection()
    {
        MaterializerKey key = new(typeof(object));
        MaterializerKey key2 = new(typeof(object));

        Assert.Equal(key, key2);
        Assert.Equal(key.GetHashCode(), key2.GetHashCode());
        Assert.True(key.Equals((object)key2));

        Assert.NotEqual(key, new MaterializerKey(typeof(string)));
        Assert.NotEqual(key.GetHashCode(), new MaterializerKey(typeof(string)).GetHashCode());
        Assert.False(key.Equals((object)new MaterializerKey(typeof(string))));
        Assert.NotEqual(key, new MaterializerKey(new FakeTypeMap()));
        Assert.NotEqual(key.GetHashCode(), new MaterializerKey(new FakeTypeMap()).GetHashCode());
        Assert.False(key.Equals((object)new MaterializerKey(new FakeTypeMap())));
    }

    [Fact]
    public static void Should_Be_Equatable_Write_SourceGen()
    {
        var tm1 = new FakeTypeMap();
        var tm2 = new AnotherFakeTypeMap();
        MaterializerKey key = new(new FakeTypeMap());
        MaterializerKey key2 = new(new FakeTypeMap());

        Assert.Equal(key, key2);
        Assert.Equal(key.GetHashCode(), key2.GetHashCode());
        Assert.True(key.Equals((object)key2));
        Assert.NotEqual(key, new MaterializerKey(tm2));
        Assert.NotEqual(key.GetHashCode(), new MaterializerKey(tm2).GetHashCode());
        Assert.False(key.Equals((object)new MaterializerKey(tm2)));
        Assert.NotEqual(key, new MaterializerKey(typeof(object)));
        Assert.NotEqual(key.GetHashCode(), new MaterializerKey(typeof(object)).GetHashCode());
        Assert.False(key.Equals((object)new MaterializerKey(typeof(object))));
        Assert.NotEqual(key, new MaterializerKey(typeof(string)));
    }

    [Fact]
    public static void Should_Use_Value_Equality_For_TypeMap()
    {
        Assert.True(new FakeTypeMap().Equals((object)new FakeTypeMap()));
        Assert.True(new FakeTypeMap { ThrowOnDuplicate = true }.Equals(new FakeTypeMap { ThrowOnDuplicate = true }));

        Assert.False(new FakeTypeMap().Equals(null));
        Assert.False(new FakeTypeMap { ThrowOnDuplicate = true }.Equals(new FakeTypeMap { ThrowOnDuplicate = false }));
    }
}

file sealed class FakeTypeMap : CsvTypeMap<char, object>;

file sealed class AnotherFakeTypeMap : CsvTypeMap<char, object>;
