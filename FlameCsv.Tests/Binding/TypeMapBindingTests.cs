using FlameCsv.Binding;
using FlameCsv.Exceptions;

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [Fact]
    public static void Should_Require_At_Least_One_Field()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            CsvReader.Read("a,b,c\r\n", ObjTypeMap_Simple.Instance, CsvOptions<char>.Default).ToList();
        });
    }

    [Fact]
    public static void Should_Throw_On_Duplicate()
    {
        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            CsvReader.Read("id,name,id\r\n", ObjTypeMap_ThrowDuplicate.Instance, CsvOptions<char>.Default).ToList();
        });
    }

    [Fact]
    public static void Should_Ignore_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" +
            "1,Bob,This value is ignored,true\r\n" +
            "2,Alice,This as well!,false\r\n";

        var items = CsvReader.Read(data, ObjTypeMap_Simple.Instance, CsvOptions<char>.Default).ToList();
        AssertItems(items);
    }

    [Fact]
    public static void Should_Throw_On_Unmatched()
    {
        const string data =
            "id,name,test,isenabled\r\n" +
            "1,Bob,This value is ignored,true\r\n" +
            "2,Alice,This as well!,false\r\n";

        Assert.ThrowsAny<CsvBindingException>(() =>
        {
            CsvReader.Read(data, ObjTypeMap_ThrowUnmatched.Instance, CsvOptions<char>.Default).ToList();
        });
    }

    [Fact]
    public static void Should_Bind_To_TypeMap()
    {
        const string data =
            "id,name,isenabled\r\n" +
            "1,Bob,true\r\n" +
            "2,Alice,false\r\n";

        var items = CsvReader.Read(data, ObjTypeMap_Simple.Instance, CsvOptions<char>.Default).ToList();
        AssertItems(items);
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
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}

[CsvTypeMap<char, TypeMapBindingTests.Obj>(IgnoreUnmatched = true)]
public partial class ObjTypeMap_Simple;

[CsvTypeMap<char, TypeMapBindingTests.Obj>(IgnoreUnmatched = false)]
public partial class ObjTypeMap_ThrowUnmatched;

[CsvTypeMap<char, TypeMapBindingTests.Obj>(ThrowOnDuplicate = true)]
public partial class ObjTypeMap_ThrowDuplicate;
