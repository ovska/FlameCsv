using System.Collections.Immutable;
using FlameCsv.Attributes;
using FlameCsv.Exceptions;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvTypeMap<char, Obj>(IgnoreUnmatched = true)]
    private partial class TypeMap;

    private sealed class Obj
    {
        [CsvIndex(0)]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string Name { get; set; } = string.Empty;

        [CsvIndex(2)]
        public bool IsEnabled { get; set; }
    }

    [Fact]
    public static void Should_Require_At_Least_One_Field()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = CsvReader.Read("a,b,c\r\n", TypeMap.Default, CsvOptions<char>.Default).ToList();
        });
    }

    [Fact]
    public static void Should_Throw_On_Duplicate()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = CsvReader
                .Read("id,name,_id\r\n", new TypeMap { ThrowOnDuplicate = true }, CsvOptions<char>.Default)
                .ToList();
        });
    }

    [Fact]
    public static void Should_Ignore_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        var items = CsvReader.Read(data, new TypeMap { IgnoreUnmatched = true }, CsvOptions<char>.Default).ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Throw_On_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = CsvReader.Read(data, new TypeMap { IgnoreUnmatched = false }, CsvOptions<char>.Default).ToList();
        });
    }

    [Fact]
    public static void Should_Bind_To_TypeMap()
    {
        const string data = "id,name,isenabled\r\n" + "1,Bob,true\r\n" + "2,Alice,false\r\n";

        var items = CsvReader.Read(data, TypeMap.Default, CsvOptions<char>.Default).ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Cache()
    {
        ImmutableArray<string> header = ["id", "name", "isenabled"];

        Assert.Same(
            TypeMap.Default.GetMaterializer(header, CsvOptions<char>.Default),
            TypeMap.Default.GetMaterializer(header, CsvOptions<char>.Default)
        );

        // only reference equality for now for options
        Assert.NotSame(
            TypeMap.Default.GetMaterializer(header, CsvOptions<char>.Default),
            TypeMap.Default.GetMaterializer(header, new CsvOptions<char>())
        );

        // typemaps use value equality (same configuration)
        Assert.Same(
            new TypeMap().GetMaterializer(header, CsvOptions<char>.Default),
            new TypeMap().GetMaterializer(header, CsvOptions<char>.Default)
        );

        // we use the comparer used by the headers, so these two are equivalent
        Assert.Same(
            TypeMap.Default.GetMaterializer(["id", "name"], CsvOptions<char>.Default),
            TypeMap.Default.GetMaterializer(["Id", "Name"], CsvOptions<char>.Default)
        );

        // test order
        Assert.NotSame(
            TypeMap.Default.GetMaterializer(["id", "name"], CsvOptions<char>.Default),
            TypeMap.Default.GetMaterializer(["name", "id"], CsvOptions<char>.Default)
        );
    }

    private static void AssertItems(List<Obj> items)
    {
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Bob", items[0].Name);
        Assert.True(items[0].IsEnabled);
        Assert.Equal(2, items[1].Id);
        Assert.Equal("Alice", items[1].Name);
        Assert.False(items[1].IsEnabled);
    }
}
