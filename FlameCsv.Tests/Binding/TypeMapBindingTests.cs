using FlameCsv.Attributes;
using FlameCsv.Exceptions;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [Fact]
    public static void Should_Require_At_Least_One_Field()
    {
        Assert.ThrowsAny<CsvBindingException>(
            () => { _ = CsvReader.Read("a,b,c\r\n", ObjTypeMap_Simple.Default, CsvOptions<char>.Default).ToList(); });
    }

    [Fact]
    public static void Should_Throw_On_Duplicate()
    {
        Assert.ThrowsAny<CsvBindingException>(
            () =>
            {
                _ = CsvReader
                    .Read("id,name,id\r\n", ObjTypeMap_ThrowDuplicate.Default, CsvOptions<char>.Default)
                    .ToList();
            });
    }

    [Fact]
    public static void Should_Ignore_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        var items = CsvReader.Read(data, ObjTypeMap_Simple.Default, CsvOptions<char>.Default).ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Throw_On_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" + "1,Bob,This value is ignored,true\r\n" + "2,Alice,This as well!,false\r\n";

        Assert.ThrowsAny<CsvBindingException>(
            () => { _ = CsvReader.Read(data, ObjTypeMap_ThrowUnmatched.Default, CsvOptions<char>.Default).ToList(); });
    }

    [Fact]
    public static void Should_Bind_To_TypeMap()
    {
        const string data = "id,name,isenabled\r\n" + "1,Bob,true\r\n" + "2,Alice,false\r\n";

        var items = CsvReader.Read(data, ObjTypeMap_Simple.Default, CsvOptions<char>.Default).ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Cache()
    {
        string[] header = ["id", "name", "isenabled"];

        Assert.Same(
            ObjTypeMap_Simple.Default.GetMaterializer(header, CsvOptions<char>.Default),
            ObjTypeMap_Simple.Default.GetMaterializer(header, CsvOptions<char>.Default));

        // only reference equality for now for options
        Assert.NotSame(
            ObjTypeMap_Simple.Default.GetMaterializer(header, CsvOptions<char>.Default),
            ObjTypeMap_Simple.Default.GetMaterializer(header, new CsvOptions<char>()));

        // different header, we can't be sure what comparer the options instance is using
        Assert.NotSame(
            ObjTypeMap_Simple.Default.GetMaterializer(["id", "name"], CsvOptions<char>.Default),
            ObjTypeMap_Simple.Default.GetMaterializer(["Id", "Name"], CsvOptions<char>.Default));

        // test order
        Assert.NotSame(
            ObjTypeMap_Simple.Default.GetMaterializer(["id", "name"], CsvOptions<char>.Default),
            ObjTypeMap_Simple.Default.GetMaterializer(["name", "id"], CsvOptions<char>.Default));

        // type map configured to not cache
        Assert.NotSame(
            ObjTypeMap_NoCache.Default.GetMaterializer(header, CsvOptions<char>.Default),
            ObjTypeMap_NoCache.Default.GetMaterializer(header, CsvOptions<char>.Default));

        var tooManyToCache = new string[32];
        tooManyToCache.AsSpan().Fill("");
        tooManyToCache[0] = "id";
        tooManyToCache[1] = "name";
        Assert.NotSame(
            ObjTypeMap_Simple.Default.GetMaterializer(tooManyToCache, CsvOptions<char>.Default),
            ObjTypeMap_Simple.Default.GetMaterializer(tooManyToCache, CsvOptions<char>.Default));
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

    public sealed class Obj
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string Name { get; set; } = string.Empty;
        [CsvIndex(2)] public bool IsEnabled { get; set; }
    }
}

[CsvTypeMap<char, TypeMapBindingTests.Obj>(IgnoreUnmatched = true)]
public partial class ObjTypeMap_Simple;

[CsvTypeMap<char, TypeMapBindingTests.Obj>(IgnoreUnmatched = false)]
public partial class ObjTypeMap_ThrowUnmatched;

[CsvTypeMap<char, TypeMapBindingTests.Obj>(ThrowOnDuplicate = true)]
public partial class ObjTypeMap_ThrowDuplicate;

[CsvTypeMap<char, TypeMapBindingTests.Obj>(NoCaching = true)]
public partial class ObjTypeMap_NoCache;
