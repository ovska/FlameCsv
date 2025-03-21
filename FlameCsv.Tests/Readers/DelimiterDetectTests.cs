using System.Buffers;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Readers;

public static class DelimiterDetectTests
{
    [Theory]
    [InlineData("sep=,\na,b,c\n1,2,3\n")]
    [InlineData("sep=;\na;b;c\n1;2;3\n")]
    [InlineData("sep=|\na|b|c\n1|2|3\n")]
    [InlineData("sep=\t\na\tb\tc\n1\t2\t3\n")]
    public static void Should_Detect_Prefix(string data)
    {
        var options = new CsvOptions<char>
        {
            Delimiter = '?', Newline = "\n", AutoDetectDelimiter = CsvDelimiterDetectionStrategy<char>.Prefix(),
        };

        var parser = CsvParser.Create(options, new ReadOnlySequence<char>(data.AsMemory()));
        List<string> fields = [];

        foreach (var record in parser.ParseRecords())
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                fields.Add(record[i].ToString());
            }
        }

        Assert.Equal(["a", "b", "c", "1", "2", "3"], fields);
    }
}
