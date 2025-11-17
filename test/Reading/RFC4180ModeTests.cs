using System.Buffers;
using System.Text;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Reading;

public class RFC4180ModeTests
{
    [Fact]
    public void Should_Handle_Alternating_Newlines()
    {
        StringBuilder sb = new();

        foreach (int i in Enumerable.Range(0, 500))
        {
            sb.Append("field1,field2,field3");
            sb.Append(i % 2 == 0 ? "\r\n" : "\n");
        }

        var result = StringBuilderSegment.Create(sb).Read(CsvOptions<char>.Default);
        Assert.Equal(
            Enumerable.Repeat("field1,field2,field3", 500).ToArray(),
            result.Select(r => string.Join(',', r)).ToArray()
        );
    }

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
        var result = input.Read(new CsvOptions<char> { Trimming = CsvFieldTrimming.Both });
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
        Assert.Equal(
            [
                ["field1", "field2", expected],
            ],
            result
        );
    }

    [Fact]
    public void Should_Handle_Segment_With_Only_CarriageReturn()
    {
        var data = MemorySegment.Create("some,line,here", "\r", "\n");
        var result = data.Read(CsvOptions<char>.Default);

        Assert.Single(result);
        Assert.Equal(["some", "line", "here"], result[0]);
    }

    [Fact]
    public void Should_Unescape_Huge_Field()
    {
        var pt1 = new string('a', 4096);
        var pt2 = new string('b', 4096);
        var hugefield = $"\"{pt1}\"\"{pt2}\"\r\n";
        Assert.Equal(8198, hugefield.Length);

        var result = hugefield.Read(new CsvOptions<char> { Newline = CsvNewline.CRLF });
        Assert.Single(result);
        Assert.Equal([pt1 + '"' + pt2], result[0]);
    }

    [Fact]
    public void Should_Unescape_Field_With_Many_Quotes()
    {
        var quotes = new string('"', 1000);
        var field = $"\"{quotes}\"";
        var result = field.Read(CsvOptions<char>.Default);
        Assert.Equal(
            [
                [new string('"', 500)],
            ],
            result
        );
    }
}

file static class Extensions
{
    public static List<List<string>> Read(in this ReadOnlySequence<char> input, CsvOptions<char> options)
    {
        List<List<string>> records = [];

        foreach (var reader in new CsvReader<char>(options, in input).ParseRecords())
        {
            List<string> fields = [];

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
