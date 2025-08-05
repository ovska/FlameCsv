using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class FieldTests
{
    [Fact]
    public static void Should_Return_End()
    {
        Assert.Equal(0, Field.End(0));
        Assert.Equal(5, Field.End(5));
        Assert.Equal(5, Field.End(5 | Field.IsEOL));
        Assert.Equal(5, Field.End(5 | Field.StartOrEnd));
    }

    [Fact]
    public static void Should_Return_NextStart()
    {
        Assert.Equal(0, Field.NextStart(Field.StartOrEnd));
        Assert.Equal(666, Field.NextStart(666 | Field.StartOrEnd));
        Assert.Equal(1, Field.NextStart(0));
        Assert.Equal(1, Field.NextStart(Field.IsEOL));
        Assert.Equal(3, Field.NextStart(2));

        Assert.Equal(3, Field.NextStart(3 | Field.StartOrEnd));
        Assert.Equal(4, Field.NextStart(3 | Field.IsEOL));
        Assert.Equal(5, Field.NextStart(3 | Field.IsCRLF));
    }
}
