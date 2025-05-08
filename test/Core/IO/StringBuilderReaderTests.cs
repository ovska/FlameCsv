using System.Text;
using FlameCsv.IO;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.IO;

public class StringBuilderReaderTests
{
    [Fact]
    public static void Should_Read_From_StringBuilder()
    {
        var data = new StringBuilder(capacity: 10)
            .Append('x', 500)
            .Append(new StringBuilder("test", capacity: 10))
            .Append(new StringBuilder(capacity: 10).Append('y', 500));

        using var reader = CsvBufferReader.Create(data);
        var result = reader.ReadToBuffer();

        var seq = StringBuilderSegment.Create(data);
        var str = seq.ToString();

        Assert.Equal(new string('x', 500) + "test" + new string('y', 500), result.ToString());
    }
}
