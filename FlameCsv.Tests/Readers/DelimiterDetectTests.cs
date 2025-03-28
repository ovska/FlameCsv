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
    [InlineData("a,b,c\n1,2,3\n")] // no prefix, but optional
    public static void Should_Detect_Prefix(string data)
    {
        var options = new CsvOptions<char>
        {
            Delimiter = ',', Newline = "\n", AutoDetectDelimiter = CsvDelimiterDetector<char>.Prefix(),
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

    [Fact]
    public static void Should_Detect_Probabilistic()
    {
        var options = new CsvOptions<char>
        {
            Delimiter = ',',
            Newline = "\n",
            AutoDetectDelimiter = CsvDelimiterDetector<char>.Values(';', '.', '|', '\t'),
        };

        var parser = CsvParser.Create(
            options,
            new ReadOnlySequence<char>(DetectionData.ReplaceLineEndings("\n").AsMemory()));

        List<string> fields = [];

        foreach (var record in parser.ParseRecords())
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                fields.Add(record[i].ToString());
            }
        }

        Assert.Equal(
            [
                "Name",
                "Age",
                "City",

                "Smith, John",
                "25.7",
                "New York",

                "Doe, Jane",
                "30.2",
                "Los|Angeles",

                "|Test",
                "40.0",
                "San|F|r|a|n|c|i|s|co|"
            ],
            fields);
    }

    private const string DetectionData =
        """
        "Name";"Age";"City"
        "Smith, John";25.7;New York
        "Doe, Jane";30.2;Los|Angeles
        "|Test";40.0;"San|F|r|a|n|c|i|s|co|"

        """;
}
