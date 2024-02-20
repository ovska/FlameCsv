namespace FlameCsv.Tests;

public static class HelperTests
{
    [Fact]
    public static void ValueList_Should_Work()
    {
        var list = new ValueList<string>
        {
            "test",
            "test2"
        };

        Assert.Equal(2, list.Size);
        Assert.Equal("test", list[0]);
        Assert.Equal("test2", list[1]);

        list = default;

        foreach (var id in Enumerable.Range(0, 50))
        {
            list.Add(id.ToString());
        }

        Assert.Equal(50, list.Size);

        foreach (var id in Enumerable.Range(0, 50))
        {
            Assert.Equal(id.ToString(), list[id]);
        }
    }
}
