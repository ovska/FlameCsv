using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class FieldTests
{
    [Fact]
    public static void Should_Return_End()
    {
        Assert.Equal(0, Field.End(0));
        Assert.Equal(5, Field.End(5));
        Assert.Equal(3, Field.End(3 | Field.IsEOL));
        Assert.Equal(3, Field.End(3 | Field.IsCRLF));
    }

    [Fact]
    public static void Should_Return_NextStart()
    {
        Assert.Equal(1, Field.NextStart(0));
        Assert.Equal(1, Field.NextStart(Field.IsEOL));
        Assert.Equal(1, Field.NextStart(Field.IsCRLF));
        Assert.Equal(2, Field.NextStartCRLFAware(Field.IsCRLF));
        Assert.Equal(3, Field.NextStart(2));
        Assert.Equal(4, Field.NextStart(3 | Field.IsEOL));
        Assert.Equal(4, Field.NextStart(3 | Field.IsCRLF));
        Assert.Equal(5, Field.NextStartCRLFAware(3 | Field.IsCRLF));
    }
}
