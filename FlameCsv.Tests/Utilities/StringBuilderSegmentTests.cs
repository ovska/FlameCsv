using System.Text;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Utilities;

public class StringBuilderSegmentTests
{
    [Fact]
    public static void Should_Return_Empty()
    {
        Assert.Equal(0, StringBuilderSegment.Create(null).Length);
        Assert.Equal(0, StringBuilderSegment.Create(new()).Length);
    }

    [Fact]
    public static void Should_Return_Single_Segment()
    {
        const string value = "Hello, World!";
        var stringBuilder = new StringBuilder(value);
        var sequence = StringBuilderSegment.Create(stringBuilder);
        Assert.Equal(value, sequence.ToString());
    }

    [Fact]
    public static void Should_Return_Multiple_Segments()
    {
        const string value = "Hello, World!";
        string padding = new(' ', 1000);
        var stringBuilder = new StringBuilder(value);
        stringBuilder.Insert(0, padding);
        var sequence = StringBuilderSegment.Create(stringBuilder);
        Assert.False(sequence.IsSingleSegment);
        Assert.Equal((padding + value), sequence.ToString());

        var segments = sequence.GetEnumerator();
        var chunks = stringBuilder.GetChunks();

        while (segments.MoveNext() && chunks.MoveNext())
        {
            Assert.Equal(chunks.Current.Span, segments.Current.Span);
        }
    }
}
