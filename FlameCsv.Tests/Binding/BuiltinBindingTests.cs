namespace FlameCsv.Tests.Binding;

public static class BuiltinBindingTests
{
    private const string data = "1,true,Alice\r\n2,false,Bob\r\n";

    [Fact]
    public static void Should_Bind_To_ValueTuple()
    {
        var items = CsvReader
            .Read<(int, bool, string)>(data, CsvTextOptions.Default, new() { HasHeader = false })
            .ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal((1, true, "Alice"), items[0]);
        Assert.Equal((2, false, "Bob"), items[1]);
    }

    [Fact]
    public static void Should_Bind_To_Tuple()
    {
        var items = CsvReader
            .Read<Tuple<int, bool, string>>(data, CsvTextOptions.Default, new() { HasHeader = false })
            .ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(new Tuple<int, bool, string>(1, true, "Alice"), items[0]);
        Assert.Equal(new Tuple<int, bool, string>(2, false, "Bob"), items[1]);
    }
}
