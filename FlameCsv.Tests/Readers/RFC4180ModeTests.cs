using System.Buffers;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class RFC4180ModeTests
{
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", " test")]
    [InlineData("\"test \"", "test ")]
    [InlineData("\" test \"", " test ")]
    public void Should_Trim_Fields(string input, string expected)
    {
        var result = input.Read(new CsvOptions<char> { Whitespace = " " });
        Assert.Single(result);
        Assert.Equal([expected], result[0]);
    }

    [Fact]
    public void Should_Seek_Long_Line()
    {
        const string input = "\"Long line with lots of content, but no quotes except the wrapping!\"";
        var result = input.Read(CsvOptions<char>.Default);

        Assert.Single(result);
        Assert.Equal([input[1..^1]], result[0]);
    }

    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public void Should_Unescape(string input, string expected)
    {
        string data = $"field1,field2,{input}";
        var result = data.Read(CsvOptions<char>.Default);
        Assert.Equal([["field1", "field2", expected]], result);
    }

    [Theory]
    [InlineData("\n", "here\r\r\r\r")]
    [InlineData("\r\n", "here\r\r\r")]
    [InlineData(null, "here\r\r\r")]
    public void Should_Handle_Segment_With_Only_CarriageReturn(string? newline, string last)
    {
        var data = MemorySegment.Create("some,line,here\r", "\r\r", "\r", "\n");
        var result = data.Read(new CsvOptions<char> { Newline = newline });

        Assert.Single(result);
        Assert.Equal(["some", "line", last], result[0]);
    }

    [Fact]
    public void Should_Unescape_Huge_Field()
    {
        var pt1 = new string('a', 4096);
        var pt2 = new string('b', 4096);
        var hugefield = $"\"{pt1}\"\"{pt2}\"\r\n";

        var result = hugefield.Read(new CsvOptions<char> { Newline = "\r\n" });
        Assert.Single(result);
        Assert.Equal([pt1 + '"' + pt2], result[0]);
    }
}

file static class Extensions
{
    public static List<List<string>> Read(in this ReadOnlySequence<char> input, CsvOptions<char> options)
    {
        using var parser = CsvParser.Create(options, in input);

        List<List<string>> records = [];
        char[] buffer = new char[64];

        while (parser.TryReadLine(out CsvFields<char> line, isFinalBlock: true) ||
               parser.TryReadLine(out line, isFinalBlock: false))
        {
            List<string> fields = [];
            var reader = new CsvFieldsRef<char>(in line, buffer);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                fields.Add(reader[i].ToString());
            }

            records.Add(fields);
        }

        return records;
    }

    public static List<List<string>> Read(this string input, CsvOptions<char> options)
    {
        return Read(new ReadOnlySequence<char>(input.AsMemory()), options);
    }
}
