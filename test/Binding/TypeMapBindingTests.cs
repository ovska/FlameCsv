using System.Collections.Immutable;
using FlameCsv.Attributes;
using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvTypeMap<char, Obj>]
    private partial class TypeMap;

    private sealed class Obj
    {
        [CsvIndex(0)]
        [CsvHeader(Aliases = ["_id"])]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string Name { get; set; } = string.Empty;

        [CsvIndex(2)]
        public bool IsEnabled { get; set; }
    }

    [CsvTypeMap<char, ObjWithRequired>]
    private partial class RequiredTypeMap;

    private sealed class ObjWithRequired(string name)
    {
        [CsvRequired]
        public int Id { get; set; }
        public string? Name { get; } = name;
        public bool IsEnabled { get; set; }
    }

    [Fact]
    public static void Should_Require_At_Least_One_Field()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = Csv.From("a,b,c\r\n")
                .Read(TypeMap.Default, new CsvOptions<char> { IgnoreDuplicateHeaders = true })
                .ToList();
        });
    }

    [Fact]
    public static void Should_Require_CsvRequired_Members()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            Csv.From("name,isenabled\r\nBob,true\r\nAlice,false\r\n")
                .Read(RequiredTypeMap.Default, CsvOptions<char>.Default)
                .ToList();
        });

        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            Csv.From("isenabled\r\true\r\false\r\n").Read(RequiredTypeMap.Default, CsvOptions<char>.Default).ToList();
        });

        var items = Csv.From("id,name\r\n1,Bob\r\n2,Alice\r\n")
            .Read(RequiredTypeMap.Default, CsvOptions<char>.Default)
            .ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Bob", items[0].Name);
        Assert.Equal(2, items[1].Id);
        Assert.Equal("Alice", items[1].Name);
    }

    [Fact]
    public static void Should_Throw_On_Duplicate()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = Csv.From("id,name,_id\r\n")
                .Read(TypeMap.Default, new CsvOptions<char> { IgnoreDuplicateHeaders = false })
                .ToList();
        });

        var valid = Csv.From("id,name,_id\r\n!unparsable!,test,6")
            .Read(TypeMap.Default, new CsvOptions<char> { IgnoreDuplicateHeaders = true })
            .ToList();

        Assert.Single(valid);
        Assert.Equal(6, valid[0].Id); // The last one wins, only the bound one is actually parsed
        Assert.Equal("test", valid[0].Name);
    }

    [Fact]
    public static void Should_Ignore_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        var items = Csv.From(data)
            .Read(TypeMap.Default, new CsvOptions<char> { IgnoreUnmatchedHeaders = true })
            .ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Throw_On_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            _ = Csv.From(data).Read(TypeMap.Default, new CsvOptions<char> { IgnoreUnmatchedHeaders = false }).ToList();
        });
    }

    [Fact]
    public static void Should_Bind_To_TypeMap()
    {
        const string data = "id,name,isenabled\r\n" + "1,Bob,true\r\n" + "2,Alice,false\r\n";

        var items = Csv.From(data).Read(TypeMap.Default, CsvOptions<char>.Default).ToList();
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
