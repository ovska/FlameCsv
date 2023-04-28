using FlameCsv.Utilities;

namespace FlameCsv.Tests;

public static class ValueArrayTests
{
    [Fact]
    public static void Should_Return_By_Index()
    {
        ValueArray<string> arr = default;

        for (int i = 0; i < 32; i++)
        {
            arr.Push(i.ToString());
            Assert.Equal(i + 1, arr.Count);
        }

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(i.ToString(), arr[i]);
        }

        arr.Reset();

        Assert.Equal(0, arr.Count);
    }

    [Fact]
    public static void Should_Validate_Index()
    {
        ValueArray<string> arr = default;

        Assert.Throws<ArgumentOutOfRangeException>(() => arr[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => arr[-1]);

        arr.Push("test");

        Assert.Throws<ArgumentOutOfRangeException>(() => arr[1]);
        Assert.Equal("test", arr[0]);

        arr.Reset();
        Assert.Throws<ArgumentOutOfRangeException>(() => arr[0]);
    }
}
