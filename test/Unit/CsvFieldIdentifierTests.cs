namespace FlameCsv.Tests;

public static class CsvFieldIdentifierTests
{
    [Fact]
    public static void Should_Return_Index()
    {
        Assert.True(new CsvFieldIdentifier(5).TryGetIndex(out int index, out _));
        Assert.Equal(5, index);
        Assert.False(new CsvFieldIdentifier("Name").TryGetIndex(out index, out _));

        Assert.True(default(CsvFieldIdentifier).TryGetIndex(out index, out _));
        Assert.Equal(0, index);

        Assert.Equal(5, new CsvFieldIdentifier(5).UnsafeIndex);
        Assert.Null(new CsvFieldIdentifier(5).UnsafeName);
    }

    [Fact]
    public static void Should_Return_Name()
    {
        Assert.True(new CsvFieldIdentifier(5).TryGetIndex(out _, out string? name));
        Assert.Null(name);
        Assert.False(new CsvFieldIdentifier("Name").TryGetIndex(out _, out name));
        Assert.Equal("Name", name);

        Assert.True(default(CsvFieldIdentifier).TryGetIndex(out _, out name));
        Assert.Null(name);

        Assert.Equal(0, new CsvFieldIdentifier("Name").UnsafeIndex);
        Assert.Equal("Name", new CsvFieldIdentifier("Name").UnsafeName);
    }

    [Theory]
    [InlineData(0, null, "CsvFieldIdentifier[0]")]
    [InlineData(0, "Name", "CsvFieldIdentifier[\"Name\"]")]
    [InlineData(5, null, "CsvFieldIdentifier[5]")]
    [InlineData(0, "", "CsvFieldIdentifier[\"\"]")]
    public static void Should_Return_String(int index, string? name, string expected)
    {
        CsvFieldIdentifier id = name is null ? index : name;
        Assert.Equal(expected, id.ToString());
    }
}
